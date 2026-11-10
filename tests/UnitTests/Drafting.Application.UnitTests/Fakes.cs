using System.Runtime.CompilerServices;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Pecas;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

internal static class Ids
{
    public static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
}

internal sealed class FakeCurrentUser(Guid? tenantId = null, Guid? userId = null) : ICurrentUser
{
    public bool IsAuthenticated => true;

    public Guid? UserId { get; } = userId ?? Ids.User;

    public Guid? TenantId { get; } = tenantId ?? Ids.Tenant;

    public string? Email => "advogada@teste.com";

    public IReadOnlyCollection<string> Roles => ["Lawyer"];

    public bool IsInRole(string role) => role == "Lawyer";
}

internal sealed class FakeClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; } = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}

internal sealed class FakeUnitOfWork : IDraftingUnitOfWork
{
    public int Saves { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;

        return Task.FromResult(1);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeRepository : ILegalDocumentRepository
{
    public List<LegalDocumentRequest> Requests { get; } = [];

    public List<RetrievedSource> Sources { get; } = [];

    public List<LegalDocumentEvent> Events { get; } = [];

    public Task<LegalDocumentRequest?> GetByIdAsync(LegalDocumentRequestId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));

    public void Add(LegalDocumentRequest entity) => Requests.Add(entity);

    public void AddChild(RetrievedSource source) => Sources.Add(source);

    public void AddChild(LegalDocumentEvent evento) => Events.Add(evento);

    public Task<LegalDocumentRequest?> FirstOrDefaultAsync(ISpecification<LegalDocumentRequest> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<LegalDocumentRequest>> ListAsync(ISpecification<LegalDocumentRequest> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<int> CountAsync(ISpecification<LegalDocumentRequest> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Update(LegalDocumentRequest entity)
    {
    }

    public void Remove(LegalDocumentRequest entity) => Requests.Remove(entity);
}

/// <summary>LLM de mentira: devolve os pedaços na ordem; opcionalmente falha ou trava depois deles.</summary>
internal sealed class FakeLlm(IReadOnlyList<string> pieces) : ILlmService
{
    public string ModelName => "qwen3:8b";

    public Exception? FailAfter { get; init; }

    public bool HangAfter { get; init; }

    public LlmChatRequest? LastRequest { get; private set; }

    public async IAsyncEnumerable<LlmChunk> ChatStreamAsync(
        LlmChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastRequest = request;

        foreach (var piece in pieces)
        {
            await Task.Yield();
            yield return new LlmChunk(piece, false, null, null);
        }

        if (FailAfter is not null)
        {
            throw FailAfter;
        }

        if (HangAfter)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        yield return new LlmChunk(string.Empty, true, 120, 340);
    }
}

internal sealed class FakeEmbeddings : IEmbeddingService
{
    public List<IReadOnlyList<string>> Calls { get; } = [];

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
    {
        Calls.Add(inputs);

        return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => new[] { 0.1f, 0.2f }).ToList());
    }
}

internal sealed class FakeVectorStore(IReadOnlyList<VectorHit>? hits = null) : IVectorStore
{
    public VectorSearchQuery? LastQuery { get; private set; }

    public List<VectorPoint> Upserted { get; } = [];

    public List<(Guid Tenant, Guid Documento)> Deleted { get; } = [];

    public int Ensured { get; private set; }

    public Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken = default)
    {
        Ensured++;

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorHit>> SearchAsync(VectorSearchQuery query, CancellationToken cancellationToken = default)
    {
        LastQuery = query;

        return Task.FromResult(hits ?? []);
    }

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
}

internal static class Samples
{
    public static VectorHit Hit(
        string id, string texto, double score, string arquivo = "contrato.pdf", int? pagina = 3,
        string? area = "civel", Guid? documento = null, string? hash = null) =>
        new(id, texto, score, new ChunkMetadata(
            documento ?? Guid.Parse("33333333-3333-3333-3333-333333333333"),
            arquivo, pagina, "contrato", area, null, null, null, hash ?? string.Empty));

    public static RetrievedChunk Chunk(string texto = "Cláusula 5: multa de 10%.", string arquivo = "contrato.pdf", int? pagina = 3) =>
        new("p1", texto, 0.9, new ChunkMetadata(null, arquivo, pagina, null, "civel", null, null, null, "h"));

    public static PecaInput Input(string fatos = "O cliente contratou o serviço e não recebeu.") =>
        new("peticao_inicial", "civel", fatos, "Condenação ao pagamento de R$ 5.000,00.", null, [], null);

    public static GerarPecaRequest Request(string fatos = "O cliente contratou o serviço e não recebeu.") =>
        new("peticao_inicial", "civel", fatos, "Condenação ao pagamento.");
}
