using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Platform.Application.Features.Me.GetMe;

public sealed class GetMeHandler(ICurrentUser currentUser) : IQueryHandler<GetMeQuery, GetMeResponse>
{
    public Task<Result<GetMeResponse>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        Result<GetMeResponse> result = new GetMeResponse(currentUser.UserId, currentUser.Email, currentUser.TenantId, currentUser.Roles);
        return Task.FromResult(result);
    }
}
