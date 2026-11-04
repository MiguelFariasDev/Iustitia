using Advocacia.Platform.Application.Features.Tenants.GetTenantById;
using Advocacia.Platform.Domain.Tenancy;
using Mapster;

namespace Advocacia.Platform.Application.Mapping;

/// <summary>
/// Mapeamento Domain -> DTO para Tenant. Só cobre a resposta de leitura "cheia"
/// (GetTenantById) — as respostas de Update/Suspend/Reactivate são só um recorte
/// pequeno e específico (Id + um ou dois campos computados), não um espelho da entidade,
/// então continuam construídas manualmente no handler.
/// </summary>
public sealed class TenantMappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Tenant, GetTenantByIdResponse>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.Cnpj, src => src.Cnpj.Value)
            .Map(dest => dest.Status, src => src.Status.ToString());
    }
}
