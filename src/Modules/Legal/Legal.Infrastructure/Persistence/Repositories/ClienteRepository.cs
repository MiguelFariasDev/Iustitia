using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;

public sealed class ClienteRepository(LegalDbContext dbContext)
    : Repository<Cliente, ClienteId>(dbContext), IClienteRepository
{
    public Task<Cliente?> GetByDocumentoAsync(Guid tenantId, Documento documento, CancellationToken cancellationToken = default) =>
        FirstOrDefaultAsync(new ClientePorDocumentoSpecification(tenantId, documento), cancellationToken);

    public Task<bool> ExistsAsync(Guid tenantId, ClienteId clienteId, CancellationToken cancellationToken = default) =>
        DbSet.AnyAsync(cliente => cliente.TenantId == tenantId && cliente.Id == clienteId, cancellationToken);

    public async Task<IReadOnlyList<Cliente>> ListAsync(
        Guid tenantId,
        TipoCliente? tipo,
        bool? apenasAtivos,
        string? busca,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await ApplyBusca(BaseQuery(tenantId, tipo, apenasAtivos), busca)
            .OrderBy(cliente => cliente.NomeBusca)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetNomesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> clienteIds, CancellationToken cancellationToken = default)
    {
        if (clienteIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = clienteIds.Select(ClienteId.From).ToList();

        var clientes = await DbSet
            .Where(cliente => cliente.TenantId == tenantId && ids.Contains(cliente.Id))
            .ToListAsync(cancellationToken);

        return clientes.ToDictionary(cliente => cliente.Id.Value, cliente => cliente.Nome.Value);
    }

    public Task<int> CountAsync(
        Guid tenantId, TipoCliente? tipo, bool? apenasAtivos, string? busca, CancellationToken cancellationToken = default) =>
        ApplyBusca(BaseQuery(tenantId, tipo, apenasAtivos), busca).CountAsync(cancellationToken);

    private IQueryable<Cliente> BaseQuery(Guid tenantId, TipoCliente? tipo, bool? apenasAtivos) =>
        DbSet.Where(cliente =>
            cliente.TenantId == tenantId
            && (tipo == null || cliente.Tipo == tipo)
            && (apenasAtivos == null || cliente.IsActive == apenasAtivos));

    /// <summary>
    /// Busca por nome ou por documento, sempre sobre a coluna DERIVADA correspondente —
    /// nunca sobre o value object convertido (ver Cliente.NomeBusca para o porquê).
    ///
    /// Um termo com 3+ dígitos é tratado como documento; qualquer outra coisa, como nome.
    /// Documento usa igualdade por prefixo (StartsWith) e não "contém": ninguém procura um
    /// CPF pelo miolo, e o prefixo aproveita o índice B-tree.
    /// </summary>
    private static IQueryable<Cliente> ApplyBusca(IQueryable<Cliente> query, string? busca)
    {
        if (string.IsNullOrWhiteSpace(busca))
        {
            return query;
        }

        var termo = busca.Trim().ToLowerInvariant();
        var digitos = new string(termo.Where(char.IsDigit).ToArray());

        return digitos.Length >= 3
            ? query.Where(cliente => cliente.DocumentoBusca.StartsWith(digitos))
            : query.Where(cliente => cliente.NomeBusca.Contains(termo));
    }
}
