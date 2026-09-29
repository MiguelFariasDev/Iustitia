using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Domain.Identity;
using MapsterMapper;

namespace Advocacia.Platform.Application.Features.Users.GetUserById;

public sealed class GetUserByIdHandler(IRepository<User, UserId> userRepository, IMapper mapper)
    : IQueryHandler<GetUserByIdQuery, GetUserByIdResponse>
{
    public async Task<Result<GetUserByIdResponse>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        // O filtro global de tenant (ITenantContext) já garante que só é possível
        // encontrar usuários do próprio escritório — ver BaseDbContext.
        var user = await userRepository.GetByIdAsync(UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<GetUserByIdResponse>(ErrorFactory.From(ErrorCode.USER_NOT_FOUND));
        }

        return mapper.Map<GetUserByIdResponse>(user);
    }
}
