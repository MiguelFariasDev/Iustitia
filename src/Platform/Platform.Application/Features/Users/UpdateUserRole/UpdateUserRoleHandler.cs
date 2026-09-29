using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Identity;

namespace Advocacia.Platform.Application.Features.Users.UpdateUserRole;

public sealed class UpdateUserRoleHandler(
    IRepository<User, UserId> userRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService) : ICommandHandler<UpdateUserRoleCommand, UpdateUserRoleResponse>
{
    public async Task<Result<UpdateUserRoleResponse>> Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var newRole))
        {
            return Result.Failure<UpdateUserRoleResponse>(ErrorFactory.From(ErrorCode.USER_ROLE_INVALID));
        }

        var user = await userRepository.GetByIdAsync(UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<UpdateUserRoleResponse>(ErrorFactory.From(ErrorCode.USER_NOT_FOUND));
        }

        var previousRole = user.Role.ToString();

        var changeRoleResult = user.ChangeRole(newRole);
        if (changeRoleResult.IsFailure)
        {
            return Result.Failure<UpdateUserRoleResponse>(changeRoleResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Updated,
            entityType: nameof(User),
            entityId: user.Id.Value.ToString(),
            before: new { Role = previousRole },
            after: new { Role = user.Role.ToString() },
            cancellationToken: cancellationToken);

        return new UpdateUserRoleResponse(user.Id.Value, user.Role.ToString());
    }
}
