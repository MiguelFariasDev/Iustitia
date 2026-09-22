using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Api.Middleware;
using Advocacia.Platform.Application;
using Advocacia.Platform.Infrastructure;
using Advocacia.Platform.Infrastructure.Auth;
using HealthChecks.NpgSql;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// TODO (Etapa 0.6): Serilog estruturado (console + Application Insights)
// TODO (Fase 1+): registrar módulos de negócio (Legal, Workflow, ...) conforme entram no roadmap

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6380";

// AddPlatformAuth precisa vir depois de AddPlatformInfrastructure: ICurrentUser/ITenantContext
// reais (baseados em HttpContext/JWT) substituem os padrões seguros (Anonymous/AsyncLocal) —
// ver Platform.Infrastructure/Auth/DependencyInjection.cs. AddPlatformApplication (MediatR,
// FluentValidation, Mapster) é independente de ASP.NET Core — ver ADR-030 — e AddPlatformApi
// só cuida de Swagger/OpenAPI.
builder.Services.AddPlatformInfrastructure(builder.Configuration);
builder.Services.AddPlatformAuth(builder.Configuration);
builder.Services.AddPlatformApplication(builder.Configuration);
builder.Services.AddPlatformApi();

builder.Services
    .AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", tags: ["ready"])
    .AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.MapPlatformEndpoints();

// /health/live: só confirma que o processo está de pé, sem checar dependências externas.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// /health/ready: checa as dependências externas (Postgres, Redis) usadas pela aplicação.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Torna a classe Program (gerada implicitamente para top-level statements) pública e
// acessível a partir de outro assembly, para uso com WebApplicationFactory<Program> nos
// testes de integração de auth/JWT/policies (ver Platform.IntegrationTests).
public partial class Program;
