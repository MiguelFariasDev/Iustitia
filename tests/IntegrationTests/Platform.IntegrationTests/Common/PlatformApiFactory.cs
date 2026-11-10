using Advocacia.Platform.Application.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Advocacia.Platform.IntegrationTests.Common;

/// <summary>
/// Sobe o host real de Platform.Api (via <c>Hosts/Api/Program.cs</c>) de ponta a ponta —
/// pipeline de middlewares, autenticação JWT, policies, endpoints Minimal API — contra um
/// Postgres real (Testcontainers, mesmo mecanismo do <see cref="PostgresFixture"/>) para os
/// testes de fluxo de autenticação/autorização (ver ADR-003/004/028/029).
///
/// Duas peças não existem neste ambiente (não há projeto Supabase real — ver
/// docs/compliance/auth-flow.md) e são substituídas por fakes/seams de teste:
/// <see cref="SupabaseAuthService"/> no lugar da chamada HTTP real ao Supabase Auth, e
/// <see cref="JwtTokenFactory"/> assinando os JWTs com uma chave RSA local em vez do
/// endpoint JWKS do Supabase — o <see cref="JwtBearerOptions"/> do host é reconfigurado
/// (via <c>PostConfigure</c>) para validar contra essa mesma chave.
/// </summary>
public sealed class PlatformApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresFixture _postgresFixture = new();

    /// <summary>Conexão como dono das tabelas (bypassa RLS) — para os testes inspecionarem o estado gravado.</summary>
    public string SuperuserConnectionString => _postgresFixture.SuperuserConnectionString;

    /// <summary>Conexão como <c>app_user</c> — a que a aplicação usa em produção, sujeita a RLS e a grants mínimos.</summary>
    public string AppUserConnectionString => _postgresFixture.AppUserConnectionString;

    public FakeSupabaseAuthService SupabaseAuthService { get; } = new();

    public TestJwtTokenFactory JwtTokenFactory { get; } = new();

    /// <summary>Conexão como dono do banco — para os testes lerem o que está de fato gravado, sem passar pela API/EF.</summary>
    public string DatabaseConnectionString => _postgresFixture.SuperuserConnectionString;

    public async Task InitializeAsync() => await _postgresFixture.InitializeAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgresFixture.DisposeAsync();
        JwtTokenFactory.Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting, e NÃO ConfigureAppConfiguration/AddInMemoryCollection: vários AddXxx de
        // infraestrutura (AddPlatformInfrastructure, AddLegalInfrastructure) leem a connection
        // string de forma EAGER, durante o registro de DI do Program.cs — antes de o
        // ConfigureAppConfiguration de um WebApplicationFactory ser aplicado ao builder
        // minimalista. Com ele, o host de teste lia o appsettings.json e conectava no banco de
        // DESENVOLVIMENTO local em vez do container de teste, gravando dados de teste nele.
        // UseSetting entra na configuração do host antes do WebApplication.CreateBuilder.
        // Dono das tabelas (bypassa RLS), como a conexão de desenvolvimento que estes testes
        // usavam na prática: vários fluxos (registro de tenant, convites) escrevem antes de
        // existir um tenant na sessão e o RLS os negaria. O isolamento por RLS é verificado
        // com app_user em TenantIsolationTests/RlsWithJwtClaimsTests; aqui valem os filtros
        // globais do EF e as policies de autorização.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgresFixture.SuperuserConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6380");
        builder.UseSetting("Supabase:Url", "https://fake-supabase.test");
        builder.UseSetting("Supabase:AnonKey", "fake-anon-key");
        builder.UseSetting("Supabase:ServiceRoleKey", "fake-service-role-key");

        builder.ConfigureServices(services =>
        {
            // Remove o HttpClient real de ISupabaseAuthService (Platform.Infrastructure) e
            // usa o fake em memória — ver FakeSupabaseAuthService.
            services.RemoveAll<ISupabaseAuthService>();
            services.AddSingleton<ISupabaseAuthService>(SupabaseAuthService);

            // Desliga o rate limiting neste host.
            //
            // O limitador conta por PROCESSO e todas as classes desta collection compartilham
            // o mesmo host e o mesmo IP de origem (TestServer), então os testes dividem um
            // único orçamento de 60 escritas/minuto. Acrescentar uma classe de teste passava
            // a derrubar outra, por 429, sem nenhuma relação com o que estava sendo testado.
            //
            // O rate limiting em si continua coberto, num host próprio e isolado — ver
            // RateLimitingTests, que existe exatamente por essa razão.
            services.Configure<RateLimiterOptions>(options =>
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    _ => RateLimitPartition.GetNoLimiter("testes")));

            // Troca a validação do JWT (JWKS real do Supabase) por a chave RSA local desta
            // factory — só depois que AddPlatformAuth já registrou o esquema JwtBearer,
            // então PostConfigure roda por último e vence.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.MetadataAddress = string.Empty;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters.ValidateIssuer = false;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidAudience = "authenticated";
                options.TokenValidationParameters.IssuerSigningKey = JwtTokenFactory.SecurityKey;
                options.TokenValidationParameters.RoleClaimType = "user_role";
                options.TokenValidationParameters.NameClaimType = "email";
            });
        });
    }
}
