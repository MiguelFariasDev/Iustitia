using Advocacia.BuildingBlocks.Domain.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Domain.Identity.Events;

namespace Advocacia.Platform.Domain.Identity;

/// <summary>
/// Usuário do escritório (advogado, sócio, estagiário, admin ou financeiro).
/// Compartilha o mesmo Id do usuário no Supabase Auth.
/// </summary>
public sealed class User : AggregateRoot<UserId>, IAuditable, ISoftDeletable, IHasTenant
{
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = null!;

    public Email Email { get; private set; } = null!;

    public OABNumber? OabNumber { get; private set; }

    /// <summary>Especialidade do advogado (ex.: "Cível", "Trabalhista") — usada pelo Kanban de Equipe.</summary>
    public string? Specialty { get; private set; }

    public UserRole Role { get; private set; }

    public string? AvatarUrl { get; private set; }

    public string? Phone { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    private User()
    {
        // EF Core.
    }

    private User(UserId id, Guid tenantId, string name, Email email, UserRole role)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Email = email;
        Role = role;
        IsActive = false;
    }

    /// <param name="id">
    /// Precisa ser o mesmo Id do usuário no Supabase Auth (ver
    /// docs/requisitos/09-modelo-de-dados.md) — nunca gerado localmente. Quem chama
    /// (o handler) é responsável por criar o usuário no Supabase Auth primeiro
    /// (SignUp/InviteUser) e passar o Id retornado por ele aqui.
    /// </param>
    /// <param name="tenantId">Escritório ao qual o usuário pertence.</param>
    /// <param name="name">Nome completo do usuário.</param>
    /// <param name="email">E-mail já validado (<see cref="Email.Create"/>).</param>
    /// <param name="role">Papel do usuário no escritório.</param>
    public static Result<User> Create(UserId id, Guid tenantId, string name, Email email, UserRole role)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<User>(ErrorFactory.From(ErrorCode.COMMON_INVALID_OPERATION));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<User>(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(name)));
        }

        var user = new User(id, tenantId, name.Trim(), email, role);
        user.RaiseDomainEvent(new UserCreatedEvent(user.Id, tenantId));

        return Result.Success(user);
    }

    /// <param name="id">Mesmo Id do usuário recém-convidado no Supabase Auth — ver <see cref="Create"/>.</param>
    /// <param name="tenantId">Escritório ao qual o usuário pertence.</param>
    /// <param name="name">Nome completo do usuário.</param>
    /// <param name="email">E-mail já validado (<see cref="Email.Create"/>).</param>
    /// <param name="role">Papel do usuário no escritório.</param>
    public static Result<User> Invite(UserId id, Guid tenantId, string name, Email email, UserRole role)
    {
        var result = Create(id, tenantId, name, email, role);
        if (result.IsFailure)
        {
            return result;
        }

        result.Value.RaiseDomainEvent(new UserInvitedEvent(result.Value.Id, tenantId, email.Value));

        return result;
    }

    public Result UpdateProfile(string name, string? phone, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.VALIDATION_REQUIRED_FIELD, nameof(name)));
        }

        Name = name.Trim();
        Phone = phone;
        AvatarUrl = avatarUrl;

        return Result.Success();
    }

    public void UpdateSpecialty(string? specialty) => Specialty = specialty;

    public Result ChangeRole(UserRole newRole)
    {
        if (Role == newRole)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.COMMON_INVALID_OPERATION));
        }

        Role = newRole;

        return Result.Success();
    }

    public Result UpdateOabNumber(OABNumber oabNumber)
    {
        OabNumber = oabNumber;
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.USER_ALREADY_ACTIVE));
        }

        IsActive = true;
        RaiseDomainEvent(new UserActivatedEvent(Id, TenantId));

        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Failure(ErrorFactory.From(ErrorCode.USER_ALREADY_INACTIVE));
        }

        IsActive = false;
        RaiseDomainEvent(new UserDeactivatedEvent(Id, TenantId));

        return Result.Success();
    }

    public void RegisterLogin(DateTimeOffset loginAt) => LastLoginAt = loginAt;
}
