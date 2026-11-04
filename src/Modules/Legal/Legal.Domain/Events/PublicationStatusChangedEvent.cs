using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.Modules.Legal.Domain.Publications;

namespace Advocacia.Modules.Legal.Domain.Events;

/// <summary>
/// O status de revisão de uma publicação mudou. É o rastro auditável da revisão humana
/// obrigatória (Provimento CFOAB nº 271/2025 — ver ADR-012): quem aprovou/corrigiu/rejeitou
/// e quando fica registrado por este evento + audit log, nunca só no estado atual da linha.
/// </summary>
public sealed record PublicationStatusChangedEvent(
    Guid PublicationId,
    Guid TenantId,
    PublicationStatus PreviousStatus,
    PublicationStatus NewStatus,
    string? Notes) : DomainEvent;
