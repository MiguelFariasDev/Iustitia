using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Identity;

namespace Advocacia.Platform.Application.Features.Auth.Login;

/// <summary>
/// Autentica no Supabase Auth e sincroniza com o registro local de User (mesmo Id —
/// ver docs/requisitos/09-modelo-de-dados.md). Login é sempre auditado, mesmo com
/// sucesso: é uma ação sensível (Provimento CFOAB/LGPD tratam login como evento
/// relevante de segurança).
/// </summary>
public sealed class LoginHandler(
    ISupabaseAuthService supabaseAuthService,
    IRepository<User, UserId> userRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var sessionResult = await supabaseAuthService.SignInAsync(request.Email, request.Password, cancellationToken);
        if (sessionResult.IsFailure)
        {
            return Result.Failure<LoginResponse>(sessionResult.Error);
        }

        var session = sessionResult.Value;

        var user = await userRepository.GetByIdAsync(UserId.From(session.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<LoginResponse>(
                ErrorFactory.From(ErrorCode.USER_NOT_FOUND));
        }

        if (!user.IsActive)
        {
            // Tentativa de login numa conta desativada é um evento de segurança relevante
            // (Provimento CFOAB/LGPD) mesmo sem sucesso — diferente de "usuário não
            // encontrado" acima, aqui já sabemos exatamente quem tentou e em qual tenant.
            await auditService.RecordAsync(
                AuditAction.LoginFailed,
                entityType: nameof(User),
                entityId: user.Id.Value.ToString(),
                performedByUserId: user.Id.Value,
                tenantId: user.TenantId,
                errorCode: ErrorCode.AUTH_ACCOUNT_DISABLED,
                cancellationToken: cancellationToken);

            return Result.Failure<LoginResponse>(
                ErrorFactory.From(ErrorCode.AUTH_ACCOUNT_DISABLED));
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

        return new LoginResponse(
            user.Id.Value,
            user.Email.Value,
            user.Name,
            user.TenantId,
            user.Role.ToString(),
            session.AccessToken,
            session.RefreshToken,
            session.ExpiresInSeconds);
    }
}
