using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;

namespace Advocacia.Platform.Application.Features.Auth.Logout;

public sealed class LogoutHandler(
    ISupabaseAuthService supabaseAuthService,
    IAuditService auditService,
    ICurrentUser currentUser) : ICommandHandler<LogoutCommand, LogoutResponse>
{
    public async Task<Result<LogoutResponse>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var result = await supabaseAuthService.SignOutAsync(request.AccessToken, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<LogoutResponse>(result.Error);
        }

        if (currentUser.UserId is { } userId)
        {
            await auditService.RecordAsync(
                AuditAction.Logout,
                entityType: "User",
                entityId: userId.ToString(),
                cancellationToken: cancellationToken);
        }

        return new LogoutResponse();
    }
}
