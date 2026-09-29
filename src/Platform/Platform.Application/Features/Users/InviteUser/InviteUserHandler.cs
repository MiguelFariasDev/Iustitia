using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Identity;

namespace Advocacia.Platform.Application.Features.Users.InviteUser;

public sealed class InviteUserHandler(
    ICurrentUser currentUser,
    ISupabaseAuthService supabaseAuthService,
    IRepository<User, UserId> userRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService) : ICommandHandler<InviteUserCommand, InviteUserResponse>
{
    public async Task<Result<InviteUserResponse>> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId)
        {
            return Result.Failure<InviteUserResponse>(ErrorFactory.From(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        }

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<InviteUserResponse>(emailResult.Error);
        }

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            return Result.Failure<InviteUserResponse>(ErrorFactory.From(ErrorCode.USER_ROLE_INVALID));
        }

        var existingUser = await userRepository.FirstOrDefaultAsync(
            new UserByEmailSpecification(tenantId, emailResult.Value), cancellationToken);
        if (existingUser is not null)
        {
            return Result.Failure<InviteUserResponse>(
                ErrorFactory.From(ErrorCode.USER_EMAIL_DUPLICATED));
        }

        var metadata = new Dictionary<string, string>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["name"] = request.Name,
            ["role"] = role.ToString(),
        };

        var inviteResult = await supabaseAuthService.InviteUserAsync(request.Email, metadata, cancellationToken);
        if (inviteResult.IsFailure)
        {
            return Result.Failure<InviteUserResponse>(inviteResult.Error);
        }

        var userResult = User.Invite(UserId.From(inviteResult.Value), tenantId, request.Name, emailResult.Value, role);
        if (userResult.IsFailure)
        {
            return Result.Failure<InviteUserResponse>(userResult.Error);
        }

        var user = userResult.Value;

        if (!string.IsNullOrWhiteSpace(request.Specialty))
        {
            user.UpdateSpecialty(request.Specialty);
        }

        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Invited,
            entityType: nameof(User),
            entityId: user.Id.Value.ToString(),
            cancellationToken: cancellationToken);

        return new InviteUserResponse(user.Id.Value, user.Email.Value);
    }

    private sealed class UserByEmailSpecification : Specification<User>
    {
        public UserByEmailSpecification(Guid tenantId, Email email)
            : base(u => u.TenantId == tenantId && u.Email == email)
        {
        }
    }
}
