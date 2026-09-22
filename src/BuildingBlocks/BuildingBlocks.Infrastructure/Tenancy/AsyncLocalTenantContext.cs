using Advocacia.BuildingBlocks.Application.Abstractions;

namespace Advocacia.BuildingBlocks.Infrastructure.Tenancy;

/// <summary>
/// Implementação de <see cref="ITenantContext"/> baseada em <see cref="AsyncLocal{T}"/>.
/// Funciona tanto em requisições HTTP (um fluxo assíncrono por requisição) quanto em
/// testes e jobs em background, sem depender de HttpContext.
///
/// A população automática a partir do JWT (via TenantContextMiddleware, em
/// Platform.Api) é implementada na Etapa 0.3. Até lá, quem precisar de um tenant
/// (testes de integração, seeds) chama <see cref="SetTenant"/> explicitamente.
/// </summary>
public sealed class AsyncLocalTenantContext : ITenantContext
{
    private static readonly AsyncLocal<Guid?> CurrentTenantId = new();

    public bool HasTenant => CurrentTenantId.Value is not null;

    public Guid TenantId =>
        CurrentTenantId.Value ?? throw new InvalidOperationException(
            "Nenhum tenant foi definido no contexto atual. Verifique HasTenant antes de acessar TenantId.");

    public void SetTenant(Guid tenantId) => CurrentTenantId.Value = tenantId;
}
