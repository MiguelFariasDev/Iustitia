using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;

namespace Advocacia.Platform.Application.Features.Auth.RefreshToken;

public sealed class RefreshTokenHandler(ISupabaseAuthService supabaseAuthService)
    : ICommandHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var result = await supabaseAuthService.RefreshTokenAsync(request.RefreshToken, cancellationToken);

        return result.IsFailure
            ? Result.Failure<RefreshTokenResponse>(result.Error)
            : new RefreshTokenResponse(result.Value.AccessToken, result.Value.RefreshToken, result.Value.ExpiresInSeconds);
    }
}
