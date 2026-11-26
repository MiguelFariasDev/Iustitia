using System.Runtime.CompilerServices;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Busca;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Seguranca;
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

    public List<CitacaoPeca> Citacoes { get; } = [];

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

    public void AddChild(CitacaoPeca citacao) => Citacoes.Add(citacao);

    public Task<LegalDocumentRequest?> GetWithCitationsAsync(LegalDocumentRequestId id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

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
    public string ModelName => "jurema-7b";

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

        return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => new float[EmbeddingConstants.Dimensoes]).ToList());
    }
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

internal sealed class MemCasos : ICasoRepository
{
    public List<Caso> Itens { get; } = [];

    public void Add(Caso caso) => Itens.Add(caso);

    public void AddMembro(CasoMembro membro)
    {
    }

    public Task<Caso?> ObterAsync(CasoId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Itens.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Caso>> ListarAsync(
        Guid? usuarioId, Guid? clienteId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Caso>>(Itens
            .Where(c => usuarioId is null || c.PodeLer(usuarioId))
            .Where(c => clienteId is null || c.ClienteId == clienteId)
            .Skip(skip).Take(take).ToList());
}

internal sealed class MemDocumentos : ICasoDocumentoRepository
{
    public List<CasoDocumento> Itens { get; } = [];

    public Dictionary<Guid, List<CasoDocumentoChunk>> Chunks { get; } = [];

    public void Add(CasoDocumento documento) => Itens.Add(documento);

    public void Remover(CasoDocumento documento)
    {
        Itens.Remove(documento);
        Chunks.Remove(documento.Id.Value);
    }

    public Task<CasoDocumento?> ObterAsync(CasoDocumentoId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Itens.FirstOrDefault(d => d.Id == id));

    public Task<CasoDocumento?> ObterPorHashAsync(CasoId casoId, string hash, CancellationToken cancellationToken = default) =>
        Task.FromResult(Itens.FirstOrDefault(d => d.CasoId == casoId && d.HashSha256 == hash));

    public Task<IReadOnlyList<CasoDocumento>> ListarAsync(CasoId casoId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CasoDocumento>>(Itens.Where(d => d.CasoId == casoId).ToList());

    public Task<IReadOnlyList<CasoDocumentoChunk>> ListarChunksAsync(CasoDocumentoId id, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CasoDocumentoChunk>>(Chunks.GetValueOrDefault(id.Value, []));

    public Task SubstituirChunksAsync(CasoDocumentoId id, IReadOnlyList<CasoDocumentoChunk> chunks, CancellationToken cancellationToken = default)
    {
        Chunks[id.Value] = [.. chunks];

        return Task.CompletedTask;
    }
}

internal sealed class MemArquivos : ICasoArquivoStore
{
    public Dictionary<string, byte[]> Objetos { get; } = [];

    public bool ForaDoAr { get; set; }

    public Task GravarAsync(string chave, byte[] conteudo, CancellationToken cancellationToken = default)
    {
        Verificar();
        Objetos[chave] = conteudo;

        return Task.CompletedTask;
    }

    public Task<byte[]?> LerAsync(string chave, CancellationToken cancellationToken = default)
    {
        Verificar();

        return Task.FromResult(Objetos.GetValueOrDefault(chave));
    }

    public Task RemoverAsync(string chave, CancellationToken cancellationToken = default)
    {
        Verificar();
        Objetos.Remove(chave);

        return Task.CompletedTask;
    }

    private void Verificar()
    {
        if (ForaDoAr)
        {
            throw new StorageException("fora do ar");
        }
    }
}

/// <summary>
/// Índice de anexos em memória. <see cref="IgnorarFiltro"/> simula o pior caso: o banco vetorial devolvendo
/// trecho de outro caso — é o que prova que a conferência da Application não depende dele.
/// </summary>
internal sealed class MemCasoVectorStore : ICasoVectorStore
{
    public List<CasoVectorPoint> Pontos { get; } = [];

    public List<FiltroCaso> Filtros { get; } = [];

    public bool IgnorarFiltro { get; set; }

    public bool ForaDoAr { get; set; }

    public bool Bm25ForaDoAr { get; set; }

    public Task UpsertAsync(IReadOnlyList<CasoVectorPoint> pontos, CancellationToken cancellationToken = default)
    {
        if (ForaDoAr)
        {
            throw new VectorStoreException("qdrant fora do ar");
        }

        Pontos.AddRange(pontos);

        return Task.CompletedTask;
    }

    public Task RemoverDocumentoAsync(Guid tenantId, Guid casoId, Guid documentoId, CancellationToken cancellationToken = default)
    {
        if (ForaDoAr)
        {
            throw new VectorStoreException("qdrant fora do ar");
        }

        Pontos.RemoveAll(p => p.Meta.Caso!.TenantId == tenantId && p.Meta.Caso.CasoId == casoId && p.Meta.Caso.DocumentoId == documentoId);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorHit>> BuscarDensoAsync(FiltroCaso filtro, float[] vetor, int limite, CancellationToken cancellationToken = default) =>
        ForaDoAr ? throw new VectorStoreException("qdrant fora do ar") : Buscar(filtro, limite);

    public Task<IReadOnlyList<VectorHit>> BuscarBm25Async(FiltroCaso filtro, VetorEsparso consulta, int limite, CancellationToken cancellationToken = default) =>
        Bm25ForaDoAr ? throw new VectorStoreException("bm25 fora do ar") : Buscar(filtro, limite);

    private Task<IReadOnlyList<VectorHit>> Buscar(FiltroCaso filtro, int limite)
    {
        Filtros.Add(filtro);

        return Task.FromResult<IReadOnlyList<VectorHit>>(Pontos
            .Where(p => IgnorarFiltro || (p.Meta.Caso!.TenantId == filtro.TenantId && p.Meta.Caso.CasoId == filtro.CasoId))
            .Take(limite)
            .Select((p, i) => new VectorHit(p.Id.ToString(), p.Texto, 0.9 - (i * 0.01), p.Meta))
            .ToList());
    }
}

internal sealed class FakeQdrant : IQdrantService
{
    public int Garantias { get; private set; }

    public Task GarantirColecoesAsync(CancellationToken cancellationToken = default)
    {
        Garantias++;

        return Task.CompletedTask;
    }

    public Task<bool> VerificarSaudeAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<IReadOnlyList<ColecaoInfo>> EstadoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ColecaoInfo>>(Colecoes.Todas.Select(c => new ColecaoInfo(c, true, 1)).ToList());
}

internal sealed class FakeSeguranca : ISegurancaIaService
{
    public List<(string Tipo, string Origem)> Eventos { get; } = [];

    public Task RegistrarAsync(Guid tenantId, Guid? usuarioId, string tipo, string origem, string? conteudo, CancellationToken cancellationToken = default)
    {
        Eventos.Add((tipo, origem));

        return Task.CompletedTask;
    }
}

/// <summary>Modelo de extração: devolve o JSON dado, ou lança.</summary>
internal sealed class FakeEstruturado(string? json = null, Exception? falha = null) : ILlmEstruturado
{
    public string? UltimoUser { get; private set; }

    public Task<string> CompletarJsonAsync(string system, string user, string jsonSchema, int maxTokens, CancellationToken cancellationToken = default)
    {
        UltimoUser = user;

        return falha is not null ? throw falha : Task.FromResult(json ?? "{}");
    }
}

/// <summary>Base jurídica de mentira: devolve sempre os mesmos trechos.</summary>
internal sealed class FakeBuscaJuridica(params VectorHit[] hits) : IBuscaJuridicaService
{
    public Task<IReadOnlyList<VectorHit>> BuscarAsync(ConsultaPreparada consulta, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VectorHit>>(hits);
}

/// <summary>Um escritório com um caso, seu cliente e a pessoa responsável.</summary>
internal sealed class Mundo
{
    public MemCasos Casos { get; } = new();

    public MemDocumentos Documentos { get; } = new();

    public MemCasoVectorStore Vetores { get; } = new();

    public FakeSeguranca Seguranca { get; } = new();

    public FakeClock Clock { get; } = new();

    public Caso Caso { get; }

    public Mundo() => Caso = NovoCaso();

    public Caso NovoCaso(Guid? tenant = null, Guid? responsavel = null, string area = "civel")
    {
        var caso = Domain.Casos.Caso.Criar(
            tenant ?? Ids.Tenant, responsavel ?? Ids.User, Guid.NewGuid(), "Cobrança", area, "Empresa X", null, null, "CE", Clock.UtcNow).Value;
        Casos.Add(caso);

        return caso;
    }

    public CasoDocumento NovoDocumento(
        Caso? caso = null, string nome = "contrato.pdf", string texto = "Cláusula 5: multa de 10% por atraso no pagamento.",
        bool restrito = false, Guid? autor = null, OrigemTexto origem = OrigemTexto.Nativo, double? confianca = null, bool suspeito = false,
        int? pagina = 3)
    {
        caso ??= Caso;
        var documento = CasoDocumento.Criar(
            CasoDocumentoId.New(), caso, autor ?? caso.UsuarioIdResponsavel, nome, FormatoDocumento.Pdf, 100,
            TextoHash.DoTexto(nome + texto + caso.Id), "chave", restrito, Clock.UtcNow);
        documento.MarcarConvertido("md", origem, confianca, 4, 1, suspeito, null);
        documento.MarcarIndexado(1, Clock.UtcNow);
        Documentos.Add(documento);
        var chunk = new CasoDocumentoChunk(documento, 0, pagina, "Cláusula 5", texto, TextoHash.DoTexto(texto), 20, origem, confianca, suspeito);
        Documentos.Chunks[documento.Id.Value] = [chunk];
        Vetores.Pontos.Add(Application.Casos.IndexacaoWorkHandler.Ponto(documento, chunk, [0f]));

        return documento;
    }

    public Application.Casos.CasoAcessoService Acesso(FakeCurrentUser user) => new(Casos, user, Seguranca);
}

internal static class Samples
{
    public static readonly Guid CasoId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    /// <summary>Trecho de anexo já recuperado.</summary>
    public static VectorHit HitCaso(
        string texto = "Cláusula 5: multa de 10%.", string arquivo = "contrato.pdf", int? pagina = 3, double score = 0.9,
        Guid? casoId = null, Guid? documentoId = null, bool suspeito = false, OrigemTexto origem = OrigemTexto.Nativo,
        double? confianca = null, bool revisaoPendente = false, string? id = null) =>
        new(id ?? Guid.NewGuid().ToString(), texto, score, new ChunkMetadata(
            documentoId, arquivo, pagina, "contrato", "civel", null, null, null, TextoHash.DoTexto(texto),
            Caso: new CasoRef(
                Ids.Tenant, casoId ?? CasoId, Guid.NewGuid(), documentoId ?? Guid.NewGuid(), Ids.User, origem, confianca, null, false,
                suspeito, revisaoPendente)));

    public static VectorHit HitNorma(
        string titulo = "Lei nº 10.406 de 10/01/2002", string tipo = "Lei Ordinária", string? numero = "10406", string? artigo = "186",
        string? texto = null, double score = 0.8, string colecao = Colecoes.LeisFederais, string situacao = "Vigente (conforme a fonte)",
        bool revogada = false, string? tribunal = null, DateOnly? data = null, string? urn = "urn:lex:br:federal:lei:2002-01-10;10406",
        string? url = "https://normas.leg.br/?urn=x", string? area = "civel", Guid? normaId = null, string? hierarquia = null, int versao = 1) =>
        new(Guid.NewGuid().ToString(), texto ?? $"{titulo} — Art. {artigo}\nAquele que, por ação ou omissão voluntária, violar direito e causar dano a outrem, comete ato ilícito.", score,
            new ChunkMetadata(
                null, titulo, null, tipo, area, tribunal,
                data ?? (colecao == Colecoes.Jurisprudencia ? null : new DateOnly(2002, 1, 10)), url,
                TextoHash.DoTexto((texto ?? titulo) + artigo),
                Norma: new NormaRef(
                    normaId ?? Guid.NewGuid(), titulo, tipo, numero, urn, situacao, revogada, "compilada (texto vigente)",
                    colecao == Colecoes.Jurisprudencia ? "jurisprudencia" : "federal", null, null, colecao, "federal/lei.md", artigo, null, null,
                    hierarquia, versao)));

    public static VectorHit HitSumula(string numero = "297", string tribunal = "STJ", string tipo = "Súmula", string? texto = null, bool revogada = false) =>
        HitNorma(
            $"Súmula nº {numero} do {tribunal}", tipo, numero, null, texto ?? "O Código de Defesa do Consumidor é aplicável às instituições financeiras.",
            0.7, Colecoes.Jurisprudencia, "Vigente", revogada, tribunal, null, null);

    public static RetrievedChunk Chunk(VectorHit hit) => new(hit.PointId, hit.Texto, hit.Score, hit.Meta);

    public static RagContext Contexto(params VectorHit[] hits) => new(hits.Select(Chunk).ToList(), 100, 0);

    public static PecaInput Input(string fatos = "O cliente contratou o serviço e não recebeu.") =>
        new("peticao_inicial", "civel", fatos, "Condenação ao pagamento de R$ 5.000,00.", null, [], null, CasoId, Guid.NewGuid());

    public static GerarPecaRequest Request(Guid casoId, string fatos = "O cliente contratou o serviço e não recebeu.") =>
        new("peticao_inicial", "civel", fatos, "Condenação ao pagamento.", CasoId: casoId);

    public static ConsultaPreparada Consulta(string tese = "responsabilidade contratual", string area = "civel", DateOnly? dataFatos = null) =>
        new(
            new ConsultaEstruturada("peticao_inicial", area, tese, ["contrato", "multa"], [], [], null, "fatos", "pedidos", [], [], [], "teste"),
            [new float[EmbeddingConstants.Dimensoes]], tese + " contrato multa", [], null, dataFatos);
}
