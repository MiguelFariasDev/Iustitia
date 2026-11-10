using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Pecas;

/// <summary>
/// Trecho recuperado do Qdrant que entrou no prompt de uma solicitação. Guardado junto da peça
/// para que o advogado (e a auditoria) saibam de onde veio cada afirmação.
/// </summary>
public sealed class RetrievedSource : Entity<Guid>, IHasTenant
{
    public const int FileNameMaxLength = 300;

    public Guid TenantId { get; private set; }

    public LegalDocumentRequestId LegalDocumentRequestId { get; private set; }

    public string QdrantPointId { get; private set; } = string.Empty;

    public Guid? DocumentoOrigemId { get; private set; }

    public string NomeArquivo { get; private set; } = string.Empty;

    public int? Pagina { get; private set; }

    public string Trecho { get; private set; } = string.Empty;

    public double Score { get; private set; }

    /// <summary>Payload do ponto (tribunal, data, urlOrigem…) como JSON, para reconstituir a fonte.</summary>
    public string? MetadadosJson { get; private set; }

    /// <summary>Verdadeiro se o texto gerado cita esta fonte (verificação feita depois da geração).</summary>
    public bool Citada { get; private set; }

    private RetrievedSource()
    {
        // EF Core.
    }

    internal RetrievedSource(
        Guid tenantId,
        LegalDocumentRequestId requestId,
        string qdrantPointId,
        Guid? documentoOrigemId,
        string nomeArquivo,
        int? pagina,
        string trecho,
        double score,
        string? metadadosJson,
        bool citada)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        LegalDocumentRequestId = requestId;
        QdrantPointId = qdrantPointId;
        DocumentoOrigemId = documentoOrigemId;
        NomeArquivo = nomeArquivo.Length > FileNameMaxLength ? nomeArquivo[..FileNameMaxLength] : nomeArquivo;
        Pagina = pagina;
        Trecho = trecho;
        Score = score;
        MetadadosJson = metadadosJson;
        Citada = citada;
    }
}
