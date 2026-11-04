using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Processos;

namespace Advocacia.Modules.Legal.Application.Abstractions;

public interface IProcessoRepository : IRepository<Processo, ProcessoId>
{
    Task<Processo?> GetByCnjAsync(Guid tenantId, string cnjDigits, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Processo>> ListAsync(
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
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Guid tenantId,
        Guid? clienteId,
        Guid? responsavelUserId,
        AreaDireito? area,
        StatusProcesso? status,
        string? busca,
        string? tribunal,
        DateOnly? distribuicaoDe,
        DateOnly? distribuicaoAte,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processos que o job deve consultar no DJEN: ativos e suspensos do escritório.
    /// É a consulta que substitui a lista fixa de números no appsettings — a captura passa a
    /// ser dirigida pelo que o escritório cadastrou.
    /// </summary>
    Task<IReadOnlyList<Processo>> ListParaCapturaAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Todos os tenants que têm ao menos um processo capturável — o job itera sobre eles.</summary>
    Task<IReadOnlyList<Guid>> ListTenantsComProcessosAtivosAsync(CancellationToken cancellationToken = default);
}
