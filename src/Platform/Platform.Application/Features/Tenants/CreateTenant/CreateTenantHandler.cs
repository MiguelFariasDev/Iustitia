using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Identity;
using Advocacia.Platform.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace Advocacia.Platform.Application.Features.Tenants.CreateTenant;

/// <summary>
/// Cadastro self-service de um novo escritório (endpoint público — ver
/// docs/compliance/auth-flow.md): cria o Tenant, provisiona o administrador no
/// Supabase Auth e cria o User local (role Owner, já ativo) com o mesmo Id do
/// Supabase — nunca gerado localmente (ver User.Create).
///
/// O escritório é criado mesmo quando o projeto exige confirmação de e-mail: nesse caso
/// o Supabase não emite sessão no cadastro, e a resposta sinaliza
/// RequiresEmailConfirmation para a interface pedir a confirmação em vez de mandar o
/// usuário para uma tela de login que vai recusá-lo.
///
/// Como a conta no Supabase nasce ANTES do commit local, qualquer falha depois dela é
/// compensada apagando essa conta: sem isso fica uma conta órfã que faz a nova tentativa
/// bater em "e-mail já cadastrado", com o escritório nunca tendo sido criado.
/// </summary>
public sealed class CreateTenantHandler(
    ISupabaseAuthService supabaseAuthService,
    IRepository<Tenant, TenantId> tenantRepository,
    IRepository<User, UserId> userRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService,
    ILogger<CreateTenantHandler> logger) : ICommandHandler<CreateTenantCommand, CreateTenantResponse>
{
    public async Task<Result<CreateTenantResponse>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AccountType>(request.AccountType, ignoreCase: true, out var accountType))
        {
            return Result.Failure<CreateTenantResponse>(ErrorFactory.From(ErrorCode.TENANT_ACCOUNT_TYPE_INVALID));
        }

        var tenantResult = CreateTenantFor(accountType, request);
        if (tenantResult.IsFailure)
        {
            return Result.Failure<CreateTenantResponse>(tenantResult.Error);
        }

        var emailResult = Email.Create(request.AdminEmail);
        if (emailResult.IsFailure)
        {
            return Result.Failure<CreateTenantResponse>(emailResult.Error);
        }

        var tenant = tenantResult.Value;

        // Chama o Supabase ANTES de tocar no repositório: se o SignUp falhar (e-mail já
        // em uso, senha fraca, etc.), nada foi rastreado no DbContext ainda para desfazer.
        var signUpResult = await supabaseAuthService.SignUpAsync(request.AdminEmail, request.AdminPassword, cancellationToken);
        if (signUpResult.IsFailure)
        {
            return Result.Failure<CreateTenantResponse>(signUpResult.Error);
        }

        var signUp = signUpResult.Value;

        var adminUserResult = User.Create(
            UserId.From(signUp.UserId), tenant.Id.Value, request.AdminName, emailResult.Value, UserRole.Owner);
        if (adminUserResult.IsFailure)
        {
            await CompensateSignUpAsync(signUp.UserId);
            return Result.Failure<CreateTenantResponse>(adminUserResult.Error);
        }

        var adminUser = adminUserResult.Value;

        // O Owner que acabou de criar o escritório e definir a própria senha entra
        // ativo direto — diferente de InviteUser, aqui não existe fluxo de convite.
        _ = adminUser.Activate();

        tenantRepository.Add(tenant);
        userRepository.Add(adminUser);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await CompensateSignUpAsync(signUp.UserId);
            throw;
        }

        await auditService.RecordAsync(
            AuditAction.Created,
            entityType: nameof(Tenant),
            entityId: tenant.Id.Value.ToString(),
            performedByUserId: adminUser.Id.Value,
            tenantId: tenant.Id.Value,
            cancellationToken: cancellationToken);

        return new CreateTenantResponse(tenant.Id.Value, adminUser.Id.Value, signUp.RequiresEmailConfirmation);
    }

    /// <summary>
    /// Apaga a conta recém-criada no Supabase depois de uma falha local. Usa
    /// CancellationToken.None de propósito: o motivo da falha pode ter sido justamente o
    /// cancelamento da requisição, e compensar com um token já cancelado não faria nada —
    /// deixando exatamente a conta órfã que este método existe para evitar.
    ///
    /// Falha ao compensar não vira erro para quem chamou: o erro que importa é o original.
    /// </summary>
    /// <summary>Cada tipo de conta valida o seu documento; o do outro tipo é ignorado.</summary>
    private static Result<Tenant> CreateTenantFor(AccountType accountType, CreateTenantCommand request)
    {
        if (accountType == AccountType.Solo)
        {
            var cpfResult = CPF.Create(request.Cpf);

            return cpfResult.IsFailure
                ? Result.Failure<Tenant>(cpfResult.Error)
                : Tenant.CreateSolo(request.Name, cpfResult.Value);
        }

        var cnpjResult = CNPJ.Create(request.Cnpj);

        return cnpjResult.IsFailure
            ? Result.Failure<Tenant>(cnpjResult.Error)
            : Tenant.Create(request.Name, cnpjResult.Value);
    }

    private async Task CompensateSignUpAsync(Guid supabaseUserId)
    {
        var result = await supabaseAuthService.DeleteUserAsync(supabaseUserId, CancellationToken.None);
        if (result.IsFailure)
        {
            // Sem dados pessoais no log — só o id técnico do Supabase.
            logger.LogError(
                "Compensação falhou: usuário {SupabaseUserId} ficou órfão no Supabase Auth após falha no cadastro de escritório. Erro: {ErrorCode}",
                supabaseUserId,
                result.Error.Code);
        }
    }
}
