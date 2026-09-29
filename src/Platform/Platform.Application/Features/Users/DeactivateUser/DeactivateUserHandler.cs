using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Identity;

namespace Advocacia.Platform.Application.Features.Users.DeactivateUser;

public sealed class DeactivateUserHandler(
    IRepository<User, UserId> userRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService,
    ICurrentUser currentUser) : ICommandHandler<DeactivateUserCommand, DeactivateUserResponse>
{
    public async Task<Result<DeactivateUserResponse>> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<DeactivateUserResponse>(ErrorFactory.From(ErrorCode.USER_NOT_FOUND));
        }

        if (currentUser.UserId == user.Id.Value)
        {
            return Result.Failure<DeactivateUserResponse>(ErrorFactory.From(ErrorCode.USER_CANNOT_DEACTIVATE_SELF));
        }

        if (user.Role == UserRole.Owner)
        {
            var otherActiveOwners = await userRepository.CountAsync(
                new OtherActiveOwnersSpecification(user.TenantId, user.Id.Value), cancellationToken);

            if (otherActiveOwners == 0)
            {
                return Result.Failure<DeactivateUserResponse>(ErrorFactory.From(ErrorCode.USER_CANNOT_REMOVE_LAST_OWNER));
            }
        }

        var deactivateResult = user.Deactivate();
        if (deactivateResult.IsFailure)
        {
            return Result.Failure<DeactivateUserResponse>(deactivateResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Deactivated,
            entityType: nameof(User),
            entityId: user.Id.Value.ToString(),
            cancellationToken: cancellationToken);

        return new DeactivateUserResponse(user.Id.Value);
    }

    private sealed class OtherActiveOwnersSpecification : Specification<User>
    {
        public OtherActiveOwnersSpecification(Guid tenantId, Guid excludingUserId)
            : base(u => u.TenantId == tenantId && u.Role == UserRole.Owner && u.IsActive && u.Id != UserId.From(excludingUserId))
        {
        }
    }
}
