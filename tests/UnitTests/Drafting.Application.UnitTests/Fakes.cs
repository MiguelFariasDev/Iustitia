using System.Runtime.CompilerServices;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Acervo;
using Advocacia.Modules.Drafting.Domain.Fila;
using Advocacia.Modules.Drafting.Domain.Pecas;

namespace Advocacia.Modules.Drafting.Application.UnitTests;

internal static class Ids
{
    public static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid OtherUser = Guid.Parse("99999999-9999-9999-9999-999999999999");
}

internal sealed class FakeCurrentUser(Guid? tenantId = null, Guid? userId = null, params string[] roles) : ICurrentUser
{
    public bool IsAuthenticated => true;

    public Guid? UserId { get; } = userId ?? Ids.User;

    public Guid? TenantId { get; } = tenantId ?? Ids.Tenant;

    public string? Email => "advogada@teste.com";

    public IReadOnlyCollection<string> Roles { get; } = roles.Length == 0 ? ["Lawyer"] : roles;

    public bool IsInRole(string role) => Roles.Contains(role);
}

internal sealed class FakeClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);

    public void Advance(TimeSpan by) => UtcNow += by;
}

internal sealed class FakeUnitOfWork : IDraftingUnitOfWork
{
    public int Saves { get; private set; }

    /// <summary>Executado a cada SaveChanges: deixa o teste simular algo que acontece "entre" dois envios (ex.: cancelamento).</summary>
    public Action? OnSave { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        OnSave?.Invoke();

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

    public Task<LegalDocumentRequest?> GetWithSourcesAsync(LegalDocumentRequestId id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<PecaSnapshot?> GetSnapshotAsync(LegalDocumentRequestId id, CancellationToken cancellationToken = default)
    {
        var r = Requests.FirstOrDefault(x => x.Id == id);

        return Task.FromResult(r is null
            ? null
            : new PecaSnapshot(r.Id.Value, r.UsuarioId, r.Status, r.EtapaAtual, r.Progresso, r.TextoGerado, r.Versao, r.Erro, r.CriadoEm, r.ConcluidoEm));
    }

    public Task<IReadOnlyList<LegalDocumentRequest>> ListRecentAsync(
        Guid? usuarioId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LegalDocumentRequest>>(Requests
            .Where(r => usuarioId == null || r.UsuarioId == usuarioId)
            .OrderByDescending(r => r.CriadoEm).Skip(skip).Take(take).ToList());

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

internal sealed class FakeIndexedDocuments : IIndexedDocumentRepository
{
    public List<IndexedDocument> Items { get; } = [];

    public Task<IndexedDocument?> GetByIdAsync(IndexedDocumentId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.Id == id));

    public Task<IndexedDocument?> FindByHashAsync(string hashConteudo, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.HashConteudo == hashConteudo));

    public Task<IReadOnlyList<IndexedDocument>> ListVisibleAsync(Guid usuarioId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IndexedDocument>>(Items
            .Where(d => d.Visibilidade == DocumentVisibility.Escritorio || d.UsuarioId == usuarioId)
            .Skip(skip).Take(take).ToList());

    public void Add(IndexedDocument entity) => Items.Add(entity);

    public void Remove(IndexedDocument entity) => Items.Remove(entity);

    public Task<IndexedDocument?> FirstOrDefaultAsync(ISpecification<IndexedDocument> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<IndexedDocument>> ListAsync(ISpecification<IndexedDocument> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<int> CountAsync(ISpecification<IndexedDocument> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Update(IndexedDocument entity)
    {
    }
}

/// <summary>Fila em memória com as MESMAS regras de elegibilidade da real (disponibilidade, reserva, tentativas).</summary>
internal sealed class FakeQueue(FakeClock clock) : IGeracaoPecaFila
{
    public sealed class Item
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public Guid TenantId { get; init; }

        public QueueItemKind Tipo { get; init; }

        public Guid ReferenciaId { get; init; }

        public QueueItemStatus Status { get; set; } = QueueItemStatus.Pendente;

        public int Tentativas { get; set; }

        public DateTimeOffset CriadoEm { get; init; }

        public DateTimeOffset DisponivelEm { get; set; }

        public DateTimeOffset? ReservadoAte { get; set; }

        public DateTimeOffset? ReservadoEm { get; set; }

        public DateTimeOffset? ConcluidoEm { get; set; }

        public string? UltimoErro { get; set; }
    }

    public List<Item> Items { get; } = [];

    public void Enfileirar(Guid tenantId, QueueItemKind tipo, Guid referenciaId) =>
        Items.Add(new Item { TenantId = tenantId, Tipo = tipo, ReferenciaId = referenciaId, CriadoEm = clock.UtcNow, DisponivelEm = clock.UtcNow });

    private bool Eligible(Item i) =>
        (i.Status == QueueItemStatus.Pendente && i.DisponivelEm <= clock.UtcNow)
        || (i.Status == QueueItemStatus.Reservado && i.ReservadoAte < clock.UtcNow);

    private static ReservedQueueItem Reserved(Item i) => new(i.Id, i.TenantId, i.Tipo, i.ReferenciaId, i.Tentativas, i.CriadoEm);

    public Task<IReadOnlyList<ReservedQueueItem>> ReservarAsync(string worker, int max, TimeSpan reserva, CancellationToken cancellationToken = default)
    {
        var picked = Items.Where(Eligible).OrderBy(i => i.CriadoEm).Take(max).ToList();
        foreach (var item in picked)
        {
            item.Status = QueueItemStatus.Reservado;
            item.Tentativas++;
            item.ReservadoEm = clock.UtcNow;
            item.ReservadoAte = clock.UtcNow + reserva;
        }

        return Task.FromResult<IReadOnlyList<ReservedQueueItem>>(picked.Select(Reserved).ToList());
    }

    public Task<IReadOnlyList<ReservedQueueItem>> ReservarExpiradosAsync(
        string worker, DateTimeOffset criadosAntes, int max, TimeSpan reserva, CancellationToken cancellationToken = default)
    {
        var picked = Items.Where(i => i.Status == QueueItemStatus.Pendente && i.CriadoEm < criadosAntes).Take(max).ToList();
        foreach (var item in picked)
        {
            item.Status = QueueItemStatus.Reservado;
            item.ReservadoAte = clock.UtcNow + reserva;
        }

        return Task.FromResult<IReadOnlyList<ReservedQueueItem>>(picked.Select(Reserved).ToList());
    }

    public Task FinalizarAsync(Guid itemId, QueueItemStatus status, string? erro, CancellationToken cancellationToken = default)
    {
        var item = Items.First(i => i.Id == itemId);
        item.Status = status;
        item.UltimoErro = erro;
        item.ConcluidoEm = clock.UtcNow;

        return Task.CompletedTask;
    }

    public Task CancelarPorReferenciaAsync(Guid referenciaId, CancellationToken cancellationToken = default)
    {
        foreach (var item in Items.Where(i => i.ReferenciaId == referenciaId && i.Status == QueueItemStatus.Pendente))
        {
            item.Status = QueueItemStatus.Cancelado;
        }

        return Task.CompletedTask;
    }

    public Task ReagendarAsync(Guid itemId, DateTimeOffset disponivelEm, string erro, bool consumirTentativa, CancellationToken cancellationToken = default)
    {
        var item = Items.First(i => i.Id == itemId);
        item.Status = QueueItemStatus.Pendente;
        item.DisponivelEm = disponivelEm;
        item.UltimoErro = erro;
        item.ReservadoAte = null;
        if (!consumirTentativa && item.Tentativas > 0)
        {
            item.Tentativas--;
        }

        return Task.CompletedTask;
    }

    public Task<int> ContarDisponiveisAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count(Eligible));

    public Task<int> ContarTrabalhoAbertoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count(i => i.Status is QueueItemStatus.Pendente or QueueItemStatus.Reservado));

    public Task<DateTimeOffset?> UltimaAtividadeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count == 0
            ? (DateTimeOffset?)null
            : Items.SelectMany(i => new[] { (DateTimeOffset?)i.CriadoEm, i.ReservadoEm, i.ConcluidoEm }).Max());
}

/// <summary>LLM de mentira: devolve os pedaços na ordem; opcionalmente falha ou trava depois deles.</summary>
internal sealed class FakeLlm(IReadOnlyList<string> pieces) : ILlmService
{
    public string ModelName => "qwen3:8b";

    public Exception? FailAfter { get; init; }

    public bool HangAfter { get; init; }

    public int Calls { get; private set; }

    public LlmChatRequest? LastRequest { get; private set; }

    public async IAsyncEnumerable<LlmChunk> ChatStreamAsync(
        LlmChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Calls++;
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

    public Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

internal sealed class FakeNotificacao : INotificacaoService
{
    public List<string> Eventos { get; } = [];

    public Task NotificarConclusaoAsync(LegalDocumentRequest request, CancellationToken cancellationToken = default)
    {
        Eventos.Add($"concluida:{request.Id.Value}");

        return Task.CompletedTask;
    }

    public Task NotificarFalhaAsync(LegalDocumentRequest request, CancellationToken cancellationToken = default)
    {
        Eventos.Add($"falha:{request.Id.Value}");

        return Task.CompletedTask;
    }

    public void NotificarOperacao(string mensagem) => Eventos.Add($"operacao:{mensagem}");
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
