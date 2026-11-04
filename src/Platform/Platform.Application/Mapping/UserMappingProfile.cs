using Advocacia.Platform.Application.Features.Users.GetUserById;
using Advocacia.Platform.Application.Features.Users.ListUsers;
using Advocacia.Platform.Domain.Identity;
using Mapster;

namespace Advocacia.Platform.Application.Mapping;

public sealed class UserMappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<User, GetUserByIdResponse>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.Email, src => src.Email.Value)
            .Map(dest => dest.Role, src => src.Role.ToString());

        config.NewConfig<User, UserListItem>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.Email, src => src.Email.Value)
            .Map(dest => dest.Role, src => src.Role.ToString());
    }
}
