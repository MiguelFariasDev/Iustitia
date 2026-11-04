using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.Modules.Legal.Domain.Publications;

namespace Advocacia.Modules.Legal.Domain.Events;

/// <summary>
/// Uma publicação nova foi capturada do diário e persistida. Gravado na outbox na mesma
/// transação da publicação (ver ADR-008) — consumidores (fila de trabalho, notificações,
/// classificação por IA) chegam nas Fases 3+.
///
/// O payload carrega identificadores e metadados, NUNCA o <c>RawContent</c>: o evento é
/// serializado em JSON numa tabela técnica e propagado por mensageria, dois lugares onde o
/// inteiro teor de uma comunicação processual não deve circular (ver claude.md, seção 14).
/// </summary>
public sealed record PublicationCapturedEvent(
    Guid PublicationId,
    Guid TenantId,
    string CnjNumber,
    string ExternalId,
    PublicationSource Source,
    DateTimeOffset PublishedAt) : DomainEvent;
