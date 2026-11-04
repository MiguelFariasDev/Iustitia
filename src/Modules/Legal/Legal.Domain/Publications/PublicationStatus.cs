namespace Advocacia.Modules.Legal.Domain.Publications;

/// <summary>
/// Estado da publicação no fluxo de revisão humana obrigatória (Provimento CFOAB
/// nº 271/2025 — ver claude.md, seção 3, e ADR-012). Toda publicação capturada nasce em
/// <see cref="PendingReview"/>: nada avança sem confirmação explícita do advogado.
/// </summary>
public enum PublicationStatus
{
    PendingReview,
    Approved,
    Corrected,
    Rejected,
    Archived,
}
