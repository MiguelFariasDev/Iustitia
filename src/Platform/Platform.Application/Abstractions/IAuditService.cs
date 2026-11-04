using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Platform.Domain.Auditing;

namespace Advocacia.Platform.Application.Abstractions;

/// <summary>
/// Registra ações sensíveis em audit_logs (login, logout, criação de tenant, convite de
/// usuário, ...). Implementação real em Platform.Infrastructure — grava via
/// IRepository{AuditLog, AuditLogId} + IUnitOfWork, usando ICurrentUser/IDateTimeProvider
/// para preencher quem fez o quê e quando.
/// </summary>
public interface IAuditService
{
    /// <param name="action">Ação sensível ocorrida.</param>
    /// <param name="entityType">Tipo da entidade afetada (ex.: "User", "Tenant").</param>
    /// <param name="entityId">Identificador da entidade afetada.</param>
    /// <param name="before">Estado anterior (serializado como JSON), quando aplicável.</param>
    /// <param name="after">Estado posterior (serializado como JSON), quando aplicável.</param>
    /// <param name="performedByUserId">
    /// Ator da ação. Omitido, usa <c>ICurrentUser.UserId</c> (caso comum: ação feita por
    /// quem já está autenticado). Precisa ser explícito no fluxo de Login: o ator é o
    /// usuário que acabou de autenticar, mas a requisição chegou anônima (sem JWT ainda).
    /// </param>
    /// <param name="tenantId">
    /// Tenant da ação. Omitido, usa <c>ICurrentUser.TenantId</c>. Precisa ser explícito nos
    /// fluxos de Login e CreateTenant, onde a requisição chega anônima (sem tenant no JWT
    /// ainda) mas o tenant já é conhecido no próprio handler.
    /// </param>
    /// <param name="errorCode">
    /// Preenchido quando a ação sensível FALHOU (ex.: tentativa de login num usuário
    /// desativado) — ver ADR-032. Gravado dentro do próprio <c>after</c> do audit log
    /// (a tabela não tem uma coluna própria para isso), nunca substitui o Result.Failure
    /// já retornado ao chamador: é só o registro histórico do que aconteceu.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task RecordAsync(
        AuditAction action,
        string entityType,
        string entityId,
        object? before = null,
        object? after = null,
        Guid? performedByUserId = null,
        Guid? tenantId = null,
        ErrorCode? errorCode = null,
        CancellationToken cancellationToken = default);
}
