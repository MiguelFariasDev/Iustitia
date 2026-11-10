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
using Advocacia.Modules.Drafting.Domain.Fila;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;
using Advocacia.Platform.Api.Extensions;
using Advocacia.Modules.Drafting.Infrastructure.Extracao;
using Advocacia.Platform.Infrastructure.Auth.Authorization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
            [new Claim(ClaimTypes.NameIdentifier, Request.Headers.TryGetValue("X-Test-User", out var user) ? user.ToString() : Ids.User.ToString()), new Claim(ClaimTypes.Role, role.ToString())], Scheme);

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

/// <summary>Identidade da requisição atual: usuário e papéis vêm das claims (cabeçalhos X-Test-*); o tenant é fixo.</summary>
internal sealed class TestCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public Guid? TenantId => Ids.Tenant;

    public string? Email => "advogada@teste.com";

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
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

    /// <summary>Atraso por pedaço: dá tempo ao teste de abrir o stream ANTES de a geração terminar.</summary>
    public int DelayMs { get; init; }

    public async IAsyncEnumerable<LlmChunk> ChatStreamAsync(
        LlmChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var piece in pieces)
        {
            await Task.Delay(DelayMs, cancellationToken);
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

    public List<(Guid Tenant, Guid Documento)> Deleted { get; } = [];

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

    public Task DeleteByDocumentAsync(Guid tenantId, Guid documentoOrigemId, CancellationToken cancellationToken = default)
    {
        Deleted.Add((tenantId, documentoOrigemId));

        return Task.CompletedTask;
    }

    public Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

/// <summary>Fila em memória (o ExecuteUpdate da real não existe no provedor em memória do EF; ela é testada no Postgres).</summary>
internal sealed class InMemoryFila : IGeracaoPecaFila
{
    public List<GenerationQueueItem> Items { get; } = [];

    public void Enfileirar(Guid tenantId, QueueItemKind tipo, Guid referenciaId) =>
        Items.Add(new GenerationQueueItem(tenantId, tipo, referenciaId, DateTimeOffset.UtcNow));

    public Task CancelarPorReferenciaAsync(Guid referenciaId, CancellationToken cancellationToken = default)
    {
        Cancelados.Add(referenciaId);

        return Task.CompletedTask;
    }

    public List<Guid> Cancelados { get; } = [];

    public Task<IReadOnlyList<ReservedQueueItem>> ReservarAsync(string worker, int max, TimeSpan reserva, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ReservedQueueItem>> ReservarExpiradosAsync(string worker, DateTimeOffset criadosAntes, int max, TimeSpan reserva, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task FinalizarAsync(Guid itemId, QueueItemStatus status, string? erro, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task ReagendarAsync(Guid itemId, DateTimeOffset disponivelEm, string erro, bool consumirTentativa, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<int> ContarDisponiveisAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items.Count);

    public Task<int> ContarTrabalhoAbertoAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items.Count);

    public Task<DateTimeOffset?> UltimaAtividadeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTimeOffset?>(null);
}

internal sealed class RecordingAlerts : IAlertPublisher
{
    public List<AlertRequest> Alerts { get; } = [];

    public Task<bool> PublishAsync(AlertRequest request, CancellationToken cancellationToken = default)
    {
        Alerts.Add(request);

        return Task.FromResult(true);
    }
}

/// <summary>
/// Sobe SÓ os endpoints de peças e do acervo num servidor em memória, com os serviços REAIS do módulo
/// (validação, serviço de peças, processador, ingestão, EF em memória) e fakes apenas nas pontas de
/// rede (Ollama, Qdrant), na fila e na identidade. Autorização, versionamento e rate limit usam os
/// mesmos nomes de policy do host real. O "worker" é simulado chamando o handler da fila à mão.
/// </summary>
internal sealed class EndpointHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private EndpointHost(WebApplication app, HttpClient client, InMemoryFila fila, RecordingAlerts alerts, StubVectorStore store)
    {
        _app = app;
        Client = client;
        Fila = fila;
        Alerts = alerts;
        Store = store;
    }

    public HttpClient Client { get; }

    public InMemoryFila Fila { get; }

    public RecordingAlerts Alerts { get; }

    public StubVectorStore Store { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<EndpointHost> StartAsync(
        ILlmService? llm = null, bool enabled = true, int rateLimitPermits = int.MaxValue, int maxMegabytes = 20)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var database = Guid.NewGuid().ToString();
        var fila = new InMemoryFila();
        var alerts = new RecordingAlerts();
        var store = new StubVectorStore();

        builder.Services.AddLogging();
        builder.Services.AddHttpContextAccessor();
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
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitingConfiguration.AiGenerationPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonimo",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitPermits,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        builder.Services.AddDbContext<DraftingDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddSingleton<ITenantContext>(new FixedTenant(Ids.Tenant));
        builder.Services.AddScoped<ICurrentUser, TestCurrentUser>();
        builder.Services.AddSingleton<IDateTimeProvider, SystemClock>();
        builder.Services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
        builder.Services.AddScoped<IIndexedDocumentRepository, IndexedDocumentRepository>();
        builder.Services.AddScoped<IDraftingUnitOfWork, DraftingUnitOfWork>();
        builder.Services.AddScoped<IPecaMetricasService, Advocacia.Modules.Drafting.Infrastructure.Metricas.PecaMetricasService>();
        builder.Services.AddSingleton<IGeracaoPecaFila>(fila);
        builder.Services.AddSingleton<IAlertPublisher>(alerts);
        builder.Services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
        builder.Services.AddDraftingApplication();
        builder.Services.Configure<DraftingOptions>(options => options.Enabled = enabled);
        builder.Services.Configure<RagOptions>(_ => { });
        builder.Services.Configure<CustoOptions>(_ => { });
        builder.Services.Configure<FilaOptions>(options =>
        {
            options.StreamIntervaloMs = 10;
            options.ParcialIntervaloMs = 0;
        });
        builder.Services.Configure<IngestaoOptions>(options => options.MaxMegabytes = maxMegabytes);

        ILlmService scripted = llm ?? new ScriptedLlm(
            ["DOS FATOS\nO autor contratou.\n", "DO DIREITO\nHá ato ilícito [contrato.pdf, p. 3].\n",
             "FONTES UTILIZADAS\ncontrato.pdf, p. 3\nPONTOS PARA REVISÃO HUMANA\nConferir."]);
        builder.Services.AddSingleton(scripted);
        builder.Services.AddSingleton<IEmbeddingService, StubEmbeddings>();
        builder.Services.AddSingleton<IVectorStore>(store);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapPecasEndpoints();
        app.MapDocumentosEndpoints();
        await app.StartAsync();

        return new EndpointHost(app, app.GetTestClient(), fila, alerts, store);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    public HttpRequestMessage Send(HttpMethod method, string url, object? body = null, string? role = "Lawyer", Guid? user = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        Authenticate(request, role, user);

        return request;
    }

    public static void Authenticate(HttpRequestMessage request, string? role, Guid? user = null)
    {
        if (role is null)
        {
            return;
        }

        request.Headers.Add("X-Test-Role", role);
        request.Headers.Add("X-Test-User", (user ?? Ids.User).ToString());
    }

    /// <summary>Faz o papel do worker: reserva e processa o item da solicitação, com o tenant dela.</summary>
    public async Task<WorkOutcome> RunWorkerAsync(Guid requestId, int tentativa = 1, CancellationToken cancellationToken = default)
    {
        await using var scope = Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IQueueWorkHandler>().First(h => h.Tipo == QueueItemKind.Geracao);
        await handler.AoReservarAsync(requestId, cancellationToken);

        return await handler.ProcessarAsync(requestId, tentativa, cancellationToken);
    }

    public async Task<WorkOutcome> RunIndexingAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var scope = Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IQueueWorkHandler>().First(h => h.Tipo == QueueItemKind.Indexacao);

        return await handler.ProcessarAsync(documentId, 1, cancellationToken);
    }
}
