using Advocacia.BuildingBlocks.Domain.Abstractions;

namespace Advocacia.Modules.Drafting.Domain.Pecas;

/// <summary>
/// Linha da trilha de auditoria de uma solicitação (recebida, recuperação concluída, geração
/// iniciada/concluída, falha). Só metadados — nunca o texto dos fatos nem da minuta.
/// </summary>
public sealed class LegalDocumentEvent : Entity<Guid>, IHasTenant
{
    public Guid TenantId { get; private set; }

    public LegalDocumentRequestId LegalDocumentRequestId { get; private set; }

    public string Tipo { get; private set; } = string.Empty;

    public DateTimeOffset OcorridoEm { get; private set; }

    /// <summary>Contadores e identificadores (JSON curto), sem dado do caso.</summary>
    public string? DetalheJson { get; private set; }

    private LegalDocumentEvent()
    {
        // EF Core.
    }

    internal LegalDocumentEvent(
        Guid tenantId, LegalDocumentRequestId requestId, string tipo, DateTimeOffset ocorridoEm, string? detalheJson)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        LegalDocumentRequestId = requestId;
        Tipo = tipo;
        OcorridoEm = ocorridoEm;
        DetalheJson = detalheJson;
    }
}
