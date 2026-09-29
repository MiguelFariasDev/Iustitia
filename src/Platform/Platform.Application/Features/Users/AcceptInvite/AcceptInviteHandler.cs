using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Identity;

namespace Advocacia.Platform.Application.Features.Users.AcceptInvite;

public sealed class AcceptInviteHandler(
    ISupabaseAuthService supabaseAuthService,
    IRepository<User, UserId> userRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService) : ICommandHandler<AcceptInviteCommand, AcceptInviteResponse>
{
    public async Task<Result<AcceptInviteResponse>> Handle(AcceptInviteCommand request, CancellationToken cancellationToken)
    {
        var supabaseUserResult = await supabaseAuthService.GetUserFromAccessTokenAsync(request.AccessToken, cancellationToken);
        if (supabaseUserResult.IsFailure)
        {
            return Result.Failure<AcceptInviteResponse>(supabaseUserResult.Error);
        }

        var user = await userRepository.GetByIdAsync(UserId.From(supabaseUserResult.Value.Id), cancellationToken);
        if (user is null)
        {
            return Result.Failure<AcceptInviteResponse>(
                ErrorFactory.From(ErrorCode.USER_NOT_FOUND));
        }

        if (user.IsActive)
        {
            return Result.Failure<AcceptInviteResponse>(
                ErrorFactory.From(ErrorCode.USER_ALREADY_ACTIVE));
        }

        var setPasswordResult = await supabaseAuthService.SetPasswordAsync(request.AccessToken, request.Password, cancellationToken);
        if (setPasswordResult.IsFailure)
        {
            return Result.Failure<AcceptInviteResponse>(setPasswordResult.Error);
        }

        var updateProfileResult = user.UpdateProfile(request.Name, user.Phone, user.AvatarUrl);
        if (updateProfileResult.IsFailure)
        {
            return Result.Failure<AcceptInviteResponse>(updateProfileResult.Error);
        }

        var activateResult = user.Activate();
        if (activateResult.IsFailure)
        {
            return Result.Failure<AcceptInviteResponse>(activateResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Activated,
            entityType: nameof(User),
            entityId: user.Id.Value.ToString(),
            performedByUserId: user.Id.Value,
            tenantId: user.TenantId,
            cancellationToken: cancellationToken);

        return new AcceptInviteResponse(user.Id.Value, user.TenantId);
    }
}
