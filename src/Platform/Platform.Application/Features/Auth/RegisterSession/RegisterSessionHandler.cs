using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Identity;

namespace Advocacia.Platform.Application.Features.Auth.RegisterSession;

/// <summary>
/// Registra o login de quem entrou direto no Supabase Auth: <c>users.last_login_at</c> e o evento
/// <see cref="AuditAction.Login"/> (Provimento CFOAB/LGPD tratam login como evento relevante de segurança).
/// A senha já foi validada pelo Supabase — o token chega aqui assinado e validado pelo middleware de JWT.
/// </summary>
public sealed class RegisterSessionHandler(
    IRepository<User, UserId> userRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<RegisterSessionCommand>
{
    public async Task<Result> Handle(RegisterSessionCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.USER_NOT_FOUND));
        }

        var user = await userRepository.GetByIdAsync(UserId.From(userId), cancellationToken);
        if (user is null)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.USER_NOT_FOUND));
        }

        if (!user.IsActive)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.AUTH_ACCOUNT_DISABLED));
        }

        user.RegisterLogin(dateTimeProvider.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuditAction.Login,
            entityType: nameof(User),
            entityId: user.Id.Value.ToString(),
            performedByUserId: user.Id.Value,
            tenantId: user.TenantId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
