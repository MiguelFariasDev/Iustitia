using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Identity;

namespace Advocacia.Platform.Application.Features.Users.UpdateUser;

public sealed class UpdateUserHandler(
    IRepository<User, UserId> userRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService) : ICommandHandler<UpdateUserCommand, UpdateUserResponse>
{
    public async Task<Result<UpdateUserResponse>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<UpdateUserResponse>(ErrorFactory.From(ErrorCode.USER_NOT_FOUND));
        }

        var before = new { user.Name, user.Specialty, user.AvatarUrl, user.Phone };

        var updateResult = user.UpdateProfile(request.Name, request.Phone, request.AvatarUrl);
        if (updateResult.IsFailure)
        {
            return Result.Failure<UpdateUserResponse>(updateResult.Error);
        }

        user.UpdateSpecialty(request.Specialty);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Updated,
            entityType: nameof(User),
            entityId: user.Id.Value.ToString(),
            before: before,
            after: new { user.Name, user.Specialty, user.AvatarUrl, user.Phone },
            cancellationToken: cancellationToken);

        return new UpdateUserResponse(user.Id.Value, user.Name);
    }
}
