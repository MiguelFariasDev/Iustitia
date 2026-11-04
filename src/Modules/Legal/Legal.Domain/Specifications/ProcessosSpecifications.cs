using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Shared;

namespace Advocacia.Modules.Legal.Domain.Specifications;

/// <summary>
/// Processos do escritório, com os filtros estruturados da listagem. A busca textual fica no
/// ProcessoRepository pelo mesmo motivo descrito em ClientesPorTenantSpecification.
/// </summary>
public sealed class ProcessosPorTenantSpecification : Specification<Processo>
{
    public ProcessosPorTenantSpecification(
        Guid tenantId,
        Guid? clienteId = null,
        Guid? responsavelUserId = null,
        AreaDireito? area = null,
        StatusProcesso? status = null,
        int? skip = null,
        int? take = null)
        : base(processo =>
            processo.TenantId == tenantId
            && (clienteId == null || processo.ClienteId == clienteId)
            && (responsavelUserId == null || processo.ResponsavelUserId == responsavelUserId)
            && (area == null || processo.Area == area)
            && (status == null || processo.Status == status))
    {
        // Mais recentes primeiro: o processo recém-distribuído é o que o escritório está
        // olhando agora.
        ApplyOrderByDescending(processo => processo.CreatedAt);

        if (skip is not null && take is not null)
        {
            ApplyPaging(skip.Value, take.Value);
        }
    }
}

/// <summary>Processo pelo número CNJ — impede cadastro duplicado no mesmo escritório.</summary>
public sealed class ProcessoPorCnjSpecification : Specification<Processo>
{
    public ProcessoPorCnjSpecification(Guid tenantId, CNJNumber cnjNumber)
        : base(processo => processo.TenantId == tenantId && processo.CnjNumber == cnjNumber)
    {
    }
}

/// <summary>
/// Processos que DEVEM ser consultados no diário: ativos e suspensos, não excluídos.
/// É esta specification que define o que a captura busca no CNJ — nenhuma publicação entra
/// na base do escritório sem um processo dele que a justifique.
/// </summary>
public sealed class ProcessosParaCapturaSpecification : Specification<Processo>
{
    public ProcessosParaCapturaSpecification(Guid tenantId)
        : base(processo =>
            processo.TenantId == tenantId
            && (processo.Status == StatusProcesso.Ativo || processo.Status == StatusProcesso.Suspenso))
    {
        ApplyOrderBy(processo => processo.CreatedAt);
    }
}
