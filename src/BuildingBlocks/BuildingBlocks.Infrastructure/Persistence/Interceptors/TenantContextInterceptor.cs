using System.Data.Common;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Advocacia.BuildingBlocks.Infrastructure.Persistence.Interceptors;

/// <summary>
/// A cada conexão aberta pelo EF Core, aplica o tenant atual (<see cref="ITenantContext"/>)
/// como variável de sessão do Postgres (<c>app.current_tenant</c>), que as políticas de
/// RLS leem via <c>current_setting('app.current_tenant')</c> — ver ADR-004. Reaplicar a
/// cada Open() (não só na primeira vez) é necessário porque o Npgsql reutiliza conexões
/// físicas do pool entre logical opens de tenants diferentes.
///
/// A interpolação de <see cref="ITenantContext.TenantId"/> (um <see cref="Guid"/>) direto
/// no SQL é segura aqui: Guid.ToString() nunca produz aspas, ponto-e-vírgula ou qualquer
/// caractere que permita injeção, e o comando SET do Postgres não aceita parâmetros.
///
/// A população de ITenantContext a partir do JWT (TenantContextMiddleware, em Platform.Api)
/// é implementada na Etapa 0.3 — este interceptor já funciona hoje com qualquer código que
/// chame ITenantContext.SetTenant (testes de integração, seeds, jobs).
/// </summary>
public sealed class TenantContextInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyTenant(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ApplyTenantAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void ApplyTenant(DbConnection connection)
    {
        if (!tenantContext.HasTenant)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"SET app.current_tenant = '{tenantContext.TenantId}'";
        command.ExecuteNonQuery();
    }

    private async Task ApplyTenantAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (!tenantContext.HasTenant)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"SET app.current_tenant = '{tenantContext.TenantId}'";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
