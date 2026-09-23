using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Users.InviteUser;
using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Users;

public class InviteUserHandlerTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISupabaseAuthService _supabaseAuthService = Substitute.For<ISupabaseAuthService>();
    private readonly IRepository<User, UserId> _userRepository = Substitute.For<IRepository<User, UserId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private readonly Guid _tenantId = Guid.NewGuid();

    public InviteUserHandlerTests() => _currentUser.TenantId.Returns(_tenantId);

    private InviteUserHandler CreateHandler() =>
        new(_currentUser, _supabaseAuthService, _userRepository, _unitOfWork, _auditService);

    [Fact]
    public async Task Handle_WithNewEmail_InvitesAndCreatesInactiveUser()
    {
        var supabaseUserId = Guid.NewGuid();
        _userRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<User>>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        _supabaseAuthService.InviteUserAsync("novo@teste.com", Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(supabaseUserId));

        var result = await CreateHandler().Handle(
            new InviteUserCommand("novo@teste.com", "Novo Usuario", "Lawyer", "Cível"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(supabaseUserId);
        _userRepository.Received(1).Add(Arg.Is<User>(u =>
            u.Id == UserId.From(supabaseUserId) && u.TenantId == _tenantId && u.Role == UserRole.Lawyer && !u.IsActive));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutTenantInCurrentUser_ReturnsUnauthorized()
    {
        _currentUser.TenantId.Returns((Guid?)null);

        var result = await CreateHandler().Handle(
            new InviteUserCommand("novo@teste.com", "Novo Usuario", "Lawyer", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
    }

    [Fact]
    public async Task Handle_WithInvalidRole_ReturnsValidationFailure()
    {
        var result = await CreateHandler().Handle(
            new InviteUserCommand("novo@teste.com", "Novo Usuario", "PapelInexistente", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_ROLE_INVALID));
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyExistsInTenant_ReturnsConflictWithoutCallingSupabase()
    {
        var existingUser = User.Create(UserId.New(), _tenantId, "Existente", Email.Create("novo@teste.com").Value, UserRole.Lawyer).Value;
        _userRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<User>>(), Arg.Any<CancellationToken>()).Returns(existingUser);

        var result = await CreateHandler().Handle(
            new InviteUserCommand("novo@teste.com", "Novo Usuario", "Lawyer", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_EMAIL_DUPLICATED));
        await _supabaseAuthService.DidNotReceiveWithAnyArgs()
            .InviteUserAsync(default!, default!, cancellationToken: default);
    }
}
