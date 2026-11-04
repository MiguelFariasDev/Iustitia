using Advocacia.Platform.Application.Features.Audit.GetAuditLog;
using Advocacia.Platform.Application.Features.Audit.ListAuditLogs;
using Advocacia.Platform.Domain.Auditing;
using Mapster;

namespace Advocacia.Platform.Application.Mapping;

public sealed class AuditLogMappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AuditLog, GetAuditLogResponse>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.Action, src => src.Action.ToString());

        config.NewConfig<AuditLog, AuditLogListItem>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.Action, src => src.Action.ToString());
    }
}
