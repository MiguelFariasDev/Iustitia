using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Acervo;

public readonly record struct IndexedDocumentId(Guid Value)
{
    public static IndexedDocumentId New() => new(Guid.NewGuid());

    public static IndexedDocumentId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public enum IndexedDocumentStatus
{
    Pendente = 0,
    Indexando = 1,
    Indexado = 2,
    Falhou = 3,
}

/// <summary>Quem pode usar o documento como fonte de uma peça.</summary>
public enum DocumentVisibility
{
    /// <summary>Todo o escritório (padrão: o acervo é do escritório).</summary>
    Escritorio = 0,

    /// <summary>Só quem enviou.</summary>
    Privado = 1,
}

/// <summary>
/// Metadados de um documento enviado ao acervo (o texto indexado vive no Qdrant). Enquanto o
/// documento não é indexado, o texto extraído fica em <see cref="PaginasJson"/>; depois de
/// indexado esse campo é APAGADO — o banco relacional fica só com metadados e hash.
/// </summary>
public sealed class IndexedDocument : AggregateRoot<IndexedDocumentId>, IAuditable, IHasTenant
{
    public const int NomeMaxLength = 300;
    public const int ErroMaxLength = 500;

    public Guid TenantId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public string NomeArquivo { get; private set; } = string.Empty;

    public string TipoDocumento { get; private set; } = string.Empty;

    public string? Area { get; private set; }

    public string? Tribunal { get; private set; }

    public DateOnly? DataPublicacao { get; private set; }

    public string? UrlOrigem { get; private set; }

    public string? TagsJson { get; private set; }

    public DocumentVisibility Visibilidade { get; private set; }

    /// <summary>SHA-256 do texto extraído normalizado: o mesmo conteúdo não entra duas vezes no mesmo escritório.</summary>
    public string HashConteudo { get; private set; } = string.Empty;

    public long TamanhoBytes { get; private set; }

    public IndexedDocumentStatus Status { get; private set; }

    public int Trechos { get; private set; }

    public string? Erro { get; private set; }

    public string? PaginasJson { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? IndexadoEm { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    private IndexedDocument()
    {
        // EF Core.
    }

    private IndexedDocument(IndexedDocumentId id)
        : base(id)
    {
    }

    public static IndexedDocument Create(
        Guid tenantId, Guid usuarioId, string nomeArquivo, string tipoDocumento, string? area, string? tribunal,
        DateOnly? dataPublicacao, string? urlOrigem, string? tagsJson, DocumentVisibility visibilidade,
        string hashConteudo, long tamanhoBytes, string paginasJson, DateTimeOffset agora) =>
        new(IndexedDocumentId.New())
        {
            TenantId = tenantId,
            UsuarioId = usuarioId,
            NomeArquivo = nomeArquivo.Length > NomeMaxLength ? nomeArquivo[..NomeMaxLength] : nomeArquivo,
            TipoDocumento = tipoDocumento,
            Area = area,
            Tribunal = tribunal,
            DataPublicacao = dataPublicacao,
            UrlOrigem = urlOrigem,
            TagsJson = tagsJson,
            Visibilidade = visibilidade,
            HashConteudo = hashConteudo,
            TamanhoBytes = tamanhoBytes,
            PaginasJson = paginasJson,
            Status = IndexedDocumentStatus.Pendente,
            CriadoEm = agora,
        };

    public void MarkIndexing() => Status = IndexedDocumentStatus.Indexando;

    public void MarkIndexed(int trechos, DateTimeOffset agora)
    {
        Status = IndexedDocumentStatus.Indexado;
        Trechos = trechos;
        IndexadoEm = agora;
        Erro = null;
        // O texto passa a viver só no banco vetorial.
        PaginasJson = null;
    }

    public void MarkFailed(string erro)
    {
        Status = IndexedDocumentStatus.Falhou;
        Erro = erro.Length > ErroMaxLength ? erro[..ErroMaxLength] : erro;
    }

    /// <summary>Volta a Pendente para nova tentativa (falha transitória).</summary>
    public void MarkPending(string erro)
    {
        Status = IndexedDocumentStatus.Pendente;
        Erro = erro.Length > ErroMaxLength ? erro[..ErroMaxLength] : erro;
    }
}
