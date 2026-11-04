using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Clientes;

namespace Advocacia.Modules.Legal.Application.Abstractions;

public interface IClienteRepository : IRepository<Cliente, ClienteId>
{
    /// <summary>Cliente com o mesmo documento no escritório — usado para impedir duplicata.</summary>
    Task<Cliente?> GetByDocumentoAsync(Guid tenantId, Documento documento, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid tenantId, ClienteId clienteId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Cliente>> ListAsync(
        Guid tenantId,
        TipoCliente? tipo,
        bool? apenasAtivos,
        string? busca,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Nome de exibição dos clientes informados, numa única consulta (para listagens).</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNomesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> clienteIds, CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Guid tenantId, TipoCliente? tipo, bool? apenasAtivos, string? busca, CancellationToken cancellationToken = default);
}
