namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Porta para criar alertas in-app a partir de qualquer módulo. Existe em BuildingBlocks
/// porque os módulos de negócio (Legal, ...) não podem depender de Platform, que é quem
/// possui e persiste os alertas (ver ADR-065). A implementação vive em Platform.Infrastructure.
///
/// Idempotente por (UserId, Type, ReferenceId): reexecutar a captura não gera alerta duplicado.
/// </summary>
public interface IAlertPublisher
{
    /// <returns>true se um alerta novo foi criado; false se já existia para a mesma referência.</returns>
    Task<bool> PublishAsync(AlertRequest request, CancellationToken cancellationToken = default);
}

/// <param name="TenantId">Escritório dono do alerta.</param>
/// <param name="UserId">Usuário que vai ver o alerta.</param>
/// <param name="Type">Nome do tipo de alerta: Publication, Task, Birthday, Movimentacao ou Other.</param>
/// <param name="Title">Título curto exibido na lista de alertas.</param>
/// <param name="Description">Detalhe opcional do alerta.</param>
/// <param name="ReferenceId">Id do objeto que originou o alerta; junto com usuário e tipo, garante idempotência.</param>
public sealed record AlertRequest(
    Guid TenantId,
    Guid UserId,
    string Type,
    string Title,
    string? Description,
    Guid? ReferenceId);
