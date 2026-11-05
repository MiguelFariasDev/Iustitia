using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Platform.Domain.Devices;

namespace Advocacia.Platform.Application.Abstractions;

/// <summary>
/// Consultas de dispositivos. As do usuário repetem tenant E usuário no filtro (além do filtro
/// global e do RLS): dispositivo é pessoal, e o isolamento por tenant sozinho deixaria um colega
/// listar ou revogar o aparelho de outro (mesma decisão de <see cref="IAlertRepository"/>, ADR-065).
/// </summary>
public interface IDeviceRepository : IRepository<Device, DeviceId>
{
    /// <summary>
    /// Por token, SEM filtrar usuário: o mesmo app pode passar de um usuário a outro do escritório
    /// (aparelho compartilhado), e o registro precisa achar a linha para reatribuí-la em vez de duplicá-la.
    /// </summary>
    Task<Device?> GetByTokenAsync(Guid tenantId, string deviceToken, CancellationToken cancellationToken = default);

    Task<Device?> GetForUserAsync(Guid tenantId, Guid userId, DeviceId id, CancellationToken cancellationToken = default);

    /// <summary>Todos os dispositivos do usuário, revogados inclusive (a tela "Segurança"), do mais recente ao mais antigo.</summary>
    Task<IReadOnlyList<Device>> ListForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Só os não revogados: o destino do push.</summary>
    Task<IReadOnlyList<Device>> ListActiveForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// `true` só se o dispositivo existe, é do tenant e está revogado. Dispositivo desconhecido NÃO é
    /// "revogado": não trancamos para fora quem enviou um id que não conhecemos.
    /// </summary>
    Task<bool> IsRevokedAsync(Guid tenantId, DeviceId id, CancellationToken cancellationToken = default);
}
