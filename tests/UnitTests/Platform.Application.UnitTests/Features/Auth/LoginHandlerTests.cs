using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Auth.Login;
using Advocacia.Platform.Domain.Auditing;
using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Auth;

public class LoginHandlerTests
{
    private readonly ISupabaseAuthService _supabaseAuthService = Substitute.For<ISupabaseAuthService>();
    private readonly IRepository<User, UserId> _userRepository = Substitute.For<IRepository<User, UserId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private LoginHandler CreateHandler() =>
        new(_supabaseAuthService, _userRepository, _unitOfWork, _auditService, _dateTimeProvider);

    private static User CreateActiveUser(Guid userId, Guid tenantId)
    {
        var user = User.Create(UserId.From(userId), tenantId, "Fulano", Email.Create("fulano@teste.com").Value, UserRole.Owner).Value;
        user.Activate();
        return user;
    }

    [Fact]
    public async Task Handle_WithValidCredentialsAndActiveUser_ReturnsSessionAndRegistersLogin()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var session = new SupabaseSession(userId, "fulano@teste.com", "access-token", "refresh-token", 3600);
        var user = CreateActiveUser(userId, tenantId);
        var now = DateTimeOffset.UtcNow;

        _supabaseAuthService.SignInAsync("fulano@teste.com", "Senha123!", Arg.Any<CancellationToken>())
            .Returns(Result.Success(session));
        _userRepository.GetByIdAsync(UserId.From(userId), Arg.Any<CancellationToken>()).Returns(user);
        _dateTimeProvider.UtcNow.Returns(now);

        var result = await CreateHandler().Handle(new LoginCommand("fulano@teste.com", "Senha123!"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.AccessToken.Should().Be("access-token");
        user.LastLoginAt.Should().Be(now);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditService.Received(1).RecordAsync(
            AuditAction.Login, nameof(User), userId.ToString(),
            before: null, after: null,
            performedByUserId: userId, tenantId: tenantId, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSupabaseSignInFails_ReturnsFailureWithoutTouchingRepository()
    {
        var error = Error.Unauthorized("auth.invalid_credentials", "Credenciais inválidas.");
        _supabaseAuthService.SignInAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<SupabaseSession>(error));

        var result = await CreateHandler().Handle(new LoginCommand("fulano@teste.com", "errada"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        await _userRepository.DidNotReceive().GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserAuthenticatedButNotProvisionedLocally_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var session = new SupabaseSession(userId, "fulano@teste.com", "access-token", "refresh-token", 3600);
        _supabaseAuthService.SignInAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(session));
        _userRepository.GetByIdAsync(UserId.From(userId), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(new LoginCommand("fulano@teste.com", "Senha123!"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_NOT_FOUND));
    }

    [Fact]
    public async Task Handle_WhenUserIsInactive_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var session = new SupabaseSession(userId, "fulano@teste.com", "access-token", "refresh-token", 3600);
        var inactiveUser = User.Create(UserId.From(userId), Guid.NewGuid(), "Fulano", Email.Create("fulano@teste.com").Value, UserRole.Lawyer).Value;

        _supabaseAuthService.SignInAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(session));
        _userRepository.GetByIdAsync(UserId.From(userId), Arg.Any<CancellationToken>()).Returns(inactiveUser);

        var result = await CreateHandler().Handle(new LoginCommand("fulano@teste.com", "Senha123!"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.AUTH_ACCOUNT_DISABLED));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditService.Received(1).RecordAsync(
            AuditAction.LoginFailed, nameof(User), userId.ToString(),
            before: null, after: null,
            performedByUserId: userId, tenantId: inactiveUser.TenantId,
            errorCode: ErrorCode.AUTH_ACCOUNT_DISABLED, cancellationToken: Arg.Any<CancellationToken>());
    }
}
