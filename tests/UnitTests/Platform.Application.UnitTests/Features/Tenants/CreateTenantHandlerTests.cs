using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.Domain.Identity;
using Advocacia.Platform.Domain.Tenancy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Tenants;

public class CreateTenantHandlerTests
{
    // CNPJ matematicamente válido (dígitos verificadores corretos) — mesmo usado em
    // exemplos de validação de CNPJ (11.444.777/0001-61).
    private const string ValidCnpj = "11444777000161";

    private readonly ISupabaseAuthService _supabaseAuthService = Substitute.For<ISupabaseAuthService>();
    private readonly IRepository<Tenant, TenantId> _tenantRepository = Substitute.For<IRepository<Tenant, TenantId>>();
    private readonly IRepository<User, UserId> _userRepository = Substitute.For<IRepository<User, UserId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly ILogger<CreateTenantHandler> _logger = Substitute.For<ILogger<CreateTenantHandler>>();

    private CreateTenantHandler CreateHandler() =>
        new(_supabaseAuthService, _tenantRepository, _userRepository, _unitOfWork, _auditService, _logger);

    private static CreateTenantCommand ValidCommand() =>
        new("Escritorio Teste", ValidCnpj, "Fulano", "fulano@teste.com", "Senha123!");

    private Guid ArrangeSignUp(bool comSessao = true)
    {
        var supabaseUserId = Guid.NewGuid();
        var session = comSessao
            ? new SupabaseSession(supabaseUserId, "fulano@teste.com", "at", "rt", 3600)
            : null;

        _supabaseAuthService.SignUpAsync("fulano@teste.com", "Senha123!", Arg.Any<CancellationToken>())
            .Returns(Result.Success(new SupabaseSignUp(supabaseUserId, "fulano@teste.com", session)));

        _supabaseAuthService.DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        return supabaseUserId;
    }

    [Fact]
    public async Task Handle_WithValidData_CreatesTenantAndActiveOwner()
    {
        var supabaseUserId = ArrangeSignUp();

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AdminUserId.Should().Be(supabaseUserId);
        result.Value.RequiresEmailConfirmation.Should().BeFalse();
        _tenantRepository.Received(1).Add(Arg.Any<Tenant>());
        _userRepository.Received(1).Add(Arg.Is<User>(u => u.Id == UserId.From(supabaseUserId) && u.Role == UserRole.Owner && u.IsActive));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidCnpj_ReturnsFailureWithoutCallingSupabase()
    {
        var command = ValidCommand() with { Cnpj = "00000000000000" };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_CNPJ_INVALID));
        await _supabaseAuthService.DidNotReceiveWithAnyArgs().SignUpAsync(default!, default!, cancellationToken: default);
    }

    private const string ValidCpf = "52998224725";

    [Fact]
    public async Task Handle_SoloWithValidCpf_CreatesSoloTenantWithoutCnpj()
    {
        ArrangeSignUp();
        var command = ValidCommand() with { AccountType = "Solo", Cnpj = null, Cpf = ValidCpf };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _tenantRepository.Received(1).Add(
            Arg.Is<Tenant>(t => t.AccountType == AccountType.Solo && t.Cnpj == null && t.Cpf != null));
        _userRepository.Received(1).Add(Arg.Is<User>(u => u.Role == UserRole.Owner && u.IsActive));
    }

    [Fact]
    public async Task Handle_SoloIgnoresAnyCnpjSent()
    {
        ArrangeSignUp();
        var command = ValidCommand() with { AccountType = "Solo", Cnpj = "00000000000000", Cpf = ValidCpf };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SoloWithInvalidCpf_FailsWithoutCallingSupabase()
    {
        var command = ValidCommand() with { AccountType = "Solo", Cnpj = null, Cpf = "11111111111" };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_CPF_INVALID));
        await _supabaseAuthService.DidNotReceiveWithAnyArgs().SignUpAsync(default!, default!, cancellationToken: default);
    }

    [Fact]
    public async Task Handle_FirmIgnoresAnyCpfSent()
    {
        ArrangeSignUp();
        var command = ValidCommand() with { Cpf = "123" };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _tenantRepository.Received(1).Add(Arg.Is<Tenant>(t => t.AccountType == AccountType.Firm && t.Cpf == null));
    }

    [Fact]
    public async Task Handle_WithUnknownAccountType_Fails()
    {
        var command = ValidCommand() with { AccountType = "Franquia" };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.TENANT_ACCOUNT_TYPE_INVALID));
    }

    [Fact]
    public async Task Handle_WithOabAndPhone_StoresThemOnTheOwner()
    {
        ArrangeSignUp();
        var command = ValidCommand() with
        {
            AdminOabNumber = "123456",
            AdminOabState = "CE",
            AdminPhone = "(85) 99999-1234",
        };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _userRepository.Received(1).Add(Arg.Is<User>(u =>
            u.OabNumber != null && u.OabNumber.Number == "123456" && u.OabNumber.StateCode == "CE"
            && u.Phone == "85999991234"));
    }

    [Fact]
    public async Task Handle_WithOabNumberButNoState_FailsWithoutCallingSupabase()
    {
        var command = ValidCommand() with { AdminOabNumber = "123456", AdminOabState = null };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.USER_OAB_INVALID));
        await _supabaseAuthService.DidNotReceiveWithAnyArgs().SignUpAsync(default!, default!, cancellationToken: default);
    }

    [Fact]
    public async Task Handle_WithoutOab_LeavesItEmpty()
    {
        ArrangeSignUp();

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _userRepository.Received(1).Add(Arg.Is<User>(u => u.OabNumber == null && u.Phone == null));
    }

    [Fact]
    public async Task Handle_WhenSupabaseSignUpFails_ReturnsFailureWithoutTouchingRepositories()
    {
        var error = Error.Conflict("auth.email_already_registered", "E-mail já cadastrado.");
        _supabaseAuthService.SignUpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<SupabaseSignUp>(error));

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        _tenantRepository.DidNotReceiveWithAnyArgs().Add(default!);
        _userRepository.DidNotReceiveWithAnyArgs().Add(default!);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Projeto Supabase com "Confirm email" ligado: o cadastro não devolve sessão. O
    /// escritório PRECISA ser criado assim mesmo — antes desta correção o handler devolvia
    /// 500 e deixava a conta órfã no Supabase.
    /// </summary>
    [Fact]
    public async Task Handle_WhenSignUpHasNoSession_CreatesTenantAndFlagsEmailConfirmation()
    {
        var supabaseUserId = ArrangeSignUp(comSessao: false);

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AdminUserId.Should().Be(supabaseUserId);
        result.Value.RequiresEmailConfirmation.Should().BeTrue();
        _tenantRepository.Received(1).Add(Arg.Any<Tenant>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _supabaseAuthService.DidNotReceiveWithAnyArgs().DeleteUserAsync(default, cancellationToken: default);
    }

    [Fact]
    public async Task Handle_WhenPersistenceFails_DeletesSupabaseUser()
    {
        var supabaseUserId = ArrangeSignUp();
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("falha no banco"));

        var act = async () => await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _supabaseAuthService.Received(1).DeleteUserAsync(supabaseUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserCreationFails_DeletesSupabaseUser()
    {
        var supabaseUserId = ArrangeSignUp();
        var command = ValidCommand() with { AdminName = "  " };

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _supabaseAuthService.Received(1).DeleteUserAsync(supabaseUserId, Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A compensação usa CancellationToken.None: a falha pode ter sido o próprio
    /// cancelamento, e compensar com um token cancelado deixaria a conta órfã.
    /// </summary>
    [Fact]
    public async Task Handle_WhenCancelled_CompensatesWithUncancelledToken()
    {
        var supabaseUserId = ArrangeSignUp();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new OperationCanceledException());

        var act = async () => await CreateHandler().Handle(ValidCommand(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _supabaseAuthService.Received(1)
            .DeleteUserAsync(supabaseUserId, Arg.Is<CancellationToken>(token => !token.IsCancellationRequested));
    }
}
