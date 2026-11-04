using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence.Repositories;

public sealed class ProcessoRepository(LegalDbContext dbContext)
    : Repository<Processo, ProcessoId>(dbContext), IProcessoRepository
{
    /// <summary>
    /// Recebe o número em qualquer formato e o canonicaliza antes de comparar: a coluna
    /// guarda a forma mascarada, então buscar pelos dígitos crus nunca casaria.
    /// Número inválido não é erro aqui — simplesmente não existe processo com ele.
    /// </summary>
    public Task<Processo?> GetByCnjAsync(Guid tenantId, string cnjDigits, CancellationToken cancellationToken = default)
    {
        var cnjNumber = CNJNumber.Create(cnjDigits);

        return cnjNumber.IsFailure
            ? Task.FromResult<Processo?>(null)
            : FirstOrDefaultAsync(new ProcessoPorCnjSpecification(tenantId, cnjNumber.Value), cancellationToken);
    }

    public async Task<IReadOnlyList<Processo>> ListAsync(
        Guid tenantId,
        Guid? clienteId,
        Guid? responsavelUserId,
        AreaDireito? area,
        StatusProcesso? status,
        string? busca,
        string? tribunal,
        DateOnly? distribuicaoDe,
        DateOnly? distribuicaoAte,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await ApplyBusca(BaseQuery(tenantId, clienteId, responsavelUserId, area, status, tribunal, distribuicaoDe, distribuicaoAte), busca)
            .OrderByDescending(processo => processo.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(
        Guid tenantId,
        Guid? clienteId,
        Guid? responsavelUserId,
        AreaDireito? area,
        StatusProcesso? status,
        string? busca,
        string? tribunal,
        DateOnly? distribuicaoDe,
        DateOnly? distribuicaoAte,
        CancellationToken cancellationToken = default) =>
        ApplyBusca(BaseQuery(tenantId, clienteId, responsavelUserId, area, status, tribunal, distribuicaoDe, distribuicaoAte), busca).CountAsync(cancellationToken);

    public Task<IReadOnlyList<Processo>> ListParaCapturaAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        ListAsync(new ProcessosParaCapturaSpecification(tenantId), cancellationToken);

    /// <summary>
    /// IgnoreQueryFilters é necessário e correto aqui: o job roda fora de qualquer requisição
    /// e precisa descobrir QUAIS tenants existem antes de poder definir um. O filtro de soft
    /// delete é reaplicado à mão para não incluir tenant cujos processos foram todos excluídos.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ListTenantsComProcessosAtivosAsync(CancellationToken cancellationToken = default) =>
        await DbSet
            .IgnoreQueryFilters()
            .Where(processo => !processo.IsDeleted
                && (processo.Status == StatusProcesso.Ativo || processo.Status == StatusProcesso.Suspenso))
            .Select(processo => processo.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

    private IQueryable<Processo> BaseQuery(
        Guid tenantId, Guid? clienteId, Guid? responsavelUserId, AreaDireito? area, StatusProcesso? status,
        string? tribunal, DateOnly? distribuicaoDe, DateOnly? distribuicaoAte) =>
        DbSet.Where(processo =>
            processo.TenantId == tenantId
            && (clienteId == null || processo.ClienteId == clienteId)
            && (responsavelUserId == null || processo.ResponsavelUserId == responsavelUserId)
            && (area == null || processo.Area == area)
            && (status == null || processo.Status == status)
            && (tribunal == null || processo.Foro.Tribunal == tribunal)
            && (distribuicaoDe == null || processo.DataDistribuicao >= distribuicaoDe)
            && (distribuicaoAte == null || processo.DataDistribuicao <= distribuicaoAte));

    /// <summary>
    /// Busca por número CNJ ou por parte contrária.
    ///
    /// O número é comparado na coluna de DÍGITOS (Processo.CnjDigits), o que torna a busca
    /// indiferente à máscara: colar "0000777" ou o número formatado encontra o mesmo processo.
    /// </summary>
    private static IQueryable<Processo> ApplyBusca(IQueryable<Processo> query, string? busca)
    {
        if (string.IsNullOrWhiteSpace(busca))
        {
            return query;
        }

        var termo = busca.Trim();
        var digitos = new string(termo.Where(char.IsDigit).ToArray());

        if (digitos.Length >= 3)
        {
            // Consulta a coluna DERIVADA de dígitos: assim tanto "0000777" quanto
            // "0000777-07.2018.8.06.0100" encontram o mesmo processo.
            return query.Where(processo => processo.CnjDigits.Contains(digitos));
        }

        var termoMinusculo = termo.ToLowerInvariant();

        return query.Where(processo =>
            processo.ParteContraria != null && processo.ParteContraria.ToLower().Contains(termoMinusculo));
    }
}
