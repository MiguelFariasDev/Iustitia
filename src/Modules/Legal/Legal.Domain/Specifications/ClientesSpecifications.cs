using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Modules.Legal.Domain.Clientes;

namespace Advocacia.Modules.Legal.Domain.Specifications;

/// <summary>
/// Clientes do escritório, com filtros de tipo e situação.
///
/// NÃO inclui busca textual de propósito. Nome e Documento são value objects mapeados por
/// HasConversion, e o tradutor de LINQ do EF Core não alcança um membro DENTRO de um valor
/// convertido: <c>cliente.Nome.Value.Contains(...)</c> compila e falha em RUNTIME com
/// "could not be translated". A busca por texto é montada no ClienteRepository com
/// <c>EF.Property</c>, que lê a coluna diretamente — e a camada de domínio segue sem
/// conhecer o EF Core.
///
/// O <c>tenantId</c> aparece no critério mesmo havendo filtro global e RLS: três camadas
/// independentes, nenhuma sendo a única a impedir vazamento (ver ADR-004).
/// </summary>
public sealed class ClientesPorTenantSpecification : Specification<Cliente>
{
    public ClientesPorTenantSpecification(
        Guid tenantId,
        TipoCliente? tipo = null,
        bool? apenasAtivos = null,
        int? skip = null,
        int? take = null)
        : base(cliente =>
            cliente.TenantId == tenantId
            && (tipo == null || cliente.Tipo == tipo)
            && (apenasAtivos == null || cliente.IsActive == apenasAtivos))
    {
        ApplyOrderBy(cliente => cliente.Nome);

        if (skip is not null && take is not null)
        {
            ApplyPaging(skip.Value, take.Value);
        }
    }
}

/// <summary>
/// Cliente pelo documento — impede cadastro duplicado no mesmo escritório. A comparação é
/// pelo value object INTEIRO (não por <c>.Value</c>), que é o que o EF Core traduz para
/// igualdade de coluna.
/// </summary>
public sealed class ClientePorDocumentoSpecification : Specification<Cliente>
{
    public ClientePorDocumentoSpecification(Guid tenantId, Documento documento)
        : base(cliente => cliente.TenantId == tenantId && cliente.Documento == documento)
    {
    }
}
