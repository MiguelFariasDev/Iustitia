using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Tenants.CreateTenant;
using Advocacia.Platform.Domain.Identity;
using Advocacia.Platform.Domain.Tenancy;
using FluentAssertions;
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

    private CreateTenantHandler CreateHandler() =>
        new(_supabaseAuthService, _tenantRepository, _userRepository, _unitOfWork, _auditService);

    private static CreateTenantCommand ValidCommand() =>
        new("Escritorio Teste", ValidCnpj, "Fulano", "fulano@teste.com", "Senha123!");

    [Fact]
    public async Task Handle_WithValidData_CreatesTenantAndActiveOwner()
    {
        var supabaseUserId = Guid.NewGuid();
        _supabaseAuthService.SignUpAsync("fulano@teste.com", "Senha123!", Arg.Any<CancellationToken>())
            .Returns(Result.Success(new SupabaseSession(supabaseUserId, "fulano@teste.com", "at", "rt", 3600)));

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AdminUserId.Should().Be(supabaseUserId);
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

    [Fact]
    public async Task Handle_WhenSupabaseSignUpFails_ReturnsFailureWithoutTouchingRepositories()
    {
        var error = Error.Conflict("auth.email_already_registered", "E-mail já cadastrado.");
        _supabaseAuthService.SignUpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<SupabaseSession>(error));

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        _tenantRepository.DidNotReceiveWithAnyArgs().Add(default!);
        _userRepository.DidNotReceiveWithAnyArgs().Add(default!);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
