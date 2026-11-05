using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Devices;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// As consultas "do usuário" repetem tenant E usuário no Where, além do filtro global e do RLS:
/// dispositivo é pessoal. Só <see cref="GetByTokenAsync"/> e <see cref="IsRevokedAsync"/> olham o
/// tenant inteiro — o primeiro para reatribuir um aparelho compartilhado, o segundo para a
/// checagem de revogação da API.
/// </summary>
public sealed class DeviceRepository(PlatformDbContext dbContext)
    : Repository<Device, DeviceId>(dbContext), IDeviceRepository
{
    public Task<Device?> GetByTokenAsync(Guid tenantId, string deviceToken, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(
            device => device.TenantId == tenantId && device.DeviceToken == deviceToken, cancellationToken);

    public Task<Device?> GetForUserAsync(Guid tenantId, Guid userId, DeviceId id, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(
            device => device.Id == id && device.TenantId == tenantId && device.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Device>> ListForUserAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(device => device.TenantId == tenantId && device.UserId == userId)
            .OrderByDescending(device => device.LastSeenAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Device>> ListActiveForUserAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(device => device.TenantId == tenantId && device.UserId == userId && !device.IsRevoked)
            .ToListAsync(cancellationToken);

    public Task<bool> IsRevokedAsync(Guid tenantId, DeviceId id, CancellationToken cancellationToken = default) =>
        DbSet.AnyAsync(
            device => device.Id == id && device.TenantId == tenantId && device.IsRevoked, cancellationToken);
}
