using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.ChangePublicationStatus;

/// <summary>
/// Revisão humana da publicação (ADR-012): é a ÚNICA forma de uma publicação sair de
/// <c>PendingReview</c>, e só por ação explícita do advogado. <paramref name="Status"/> é o nome
/// de <c>PublicationStatus</c>; <paramref name="Notes"/> registra o porquê da decisão.
/// </summary>
public sealed record ChangePublicationStatusCommand(Guid PublicationId, string Status, string? Notes = null) : ICommand;
