using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Api.UnitTests;

internal static class Ids
{
    public static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
}

/// <summary>Autenticação de teste: o papel vem do cabeçalho X-Test-Role; sem cabeçalho, anônimo (401).</summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Ids.User.ToString()), new Claim(ClaimTypes.Role, role.ToString())], Scheme);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}

internal sealed class FixedTenant(Guid tenantId) : ITenantContext
{
    public bool HasTenant => true;

    public Guid TenantId { get; } = tenantId;

    public void SetTenant(Guid tenantId) => throw new NotSupportedException();
}

internal sealed class FixedUser : ICurrentUser
{
    public bool IsAuthenticated => true;

    public Guid? UserId => Ids.User;

    public Guid? TenantId => Ids.Tenant;

    public string? Email => "advogada@teste.com";

    public IReadOnlyCollection<string> Roles => ["Lawyer"];

    public bool IsInRole(string role) => role == "Lawyer";
}

internal sealed class SystemClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}

internal sealed class ScriptedLlm(IReadOnlyList<string> pieces) : ILlmService
{
    public string ModelName => "qwen3:8b";

    public Exception? FailAfter { get; init; }

    public async IAsyncEnumerable<LlmChunk> ChatStreamAsync(
        LlmChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var piece in pieces)
        {
            await Task.Yield();
            yield return new LlmChunk(piece, false, null, null);
        }

        if (FailAfter is not null)
        {
            throw FailAfter;
        }

        yield return new LlmChunk(string.Empty, true, 10, 20);
    }
}

internal sealed class StubEmbeddings : IEmbeddingService
{
    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => new[] { 0.1f }).ToList());
}

internal sealed class StubVectorStore : IVectorStore
{
    public List<VectorPoint> Upserted { get; } = [];

    public Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<VectorHit>> SearchAsync(VectorSearchQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VectorHit>>(
        [
            new VectorHit(
                "11111111-2222-3333-4444-555555555555", "Art. 186: ato ilícito.", 0.9,
                new ChunkMetadata(Guid.NewGuid(), "contrato.pdf", 3, "contrato", "civel", null, null, null, "H1")),
        ]);

    public Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default)
    {
        Upserted.AddRange(points);

        return Task.CompletedTask;
    }

    public Task DeleteByDocumentAsync(Guid tenantId, Guid documentoOrigemId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

/// <summary>
/// Sobe SÓ os endpoints de peças num servidor em memória, com os serviços REAIS do módulo
/// (validação, RAG, prompt, orquestração, persistência no EF em memória) e fakes apenas nas pontas
/// de rede (Ollama, Qdrant) e na identidade. Autorização, versionamento e rate limit usam os mesmos
/// nomes de policy do host real.
/// </summary>
internal sealed class EndpointHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private EndpointHost(WebApplication app, HttpClient client)
    {
        _app = app;
        Client = client;
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<EndpointHost> StartAsync(
        ILlmService? llm = null, bool enabled = true, StubVectorStore? store = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var database = Guid.NewGuid().ToString();

        builder.Services.AddLogging();
        builder.Services.AddAuthentication(TestAuthHandler.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.LawyerOrAbove, policy => policy.RequireRole("Owner", "Partner", "Lawyer"));
            options.AddPolicy(Policies.PartnerOrAbove, policy => policy.RequireRole("Owner", "Partner"));
        });
        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });
        builder.Services.AddRateLimiter(options =>
            options.AddPolicy(RateLimitingConfiguration.AiGenerationPolicy, _ => RateLimitPartition.GetNoLimiter("teste")));

        builder.Services.AddDbContext<DraftingDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddSingleton<ITenantContext>(new FixedTenant(Ids.Tenant));
        builder.Services.AddSingleton<ICurrentUser, FixedUser>();
        builder.Services.AddSingleton<IDateTimeProvider, SystemClock>();
        builder.Services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
        builder.Services.AddScoped<IDraftingUnitOfWork, DraftingUnitOfWork>();
        builder.Services.AddDraftingApplication();
        builder.Services.Configure<DraftingOptions>(options => options.Enabled = enabled);
        builder.Services.Configure<RagOptions>(_ => { });

        ILlmService scripted = llm ?? new ScriptedLlm(
            ["DOS FATOS\nO autor contratou.\n", "DO DIREITO\nHá ato ilícito [contrato.pdf, p. 3].\n",
             "FONTES UTILIZADAS\ncontrato.pdf, p. 3\nPONTOS PARA REVISÃO HUMANA\nConferir."]);
        builder.Services.AddSingleton(scripted);
        builder.Services.AddSingleton<IEmbeddingService, StubEmbeddings>();
        builder.Services.AddSingleton<IVectorStore>(store ?? new StubVectorStore());

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapPecasEndpoints();
        await app.StartAsync();

        return new EndpointHost(app, app.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    public HttpRequestMessage Post(string url, object body, string? role = "Lawyer")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (role is not null)
        {
            request.Headers.Add("X-Test-Role", role);
        }

        return request;
    }
}
