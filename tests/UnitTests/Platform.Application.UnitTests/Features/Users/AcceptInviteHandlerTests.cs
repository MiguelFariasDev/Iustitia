using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Users.AcceptInvite;
using Advocacia.Platform.Domain.Identity;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Users;

public class AcceptInviteHandlerTests
{
    private readonly ISupabaseAuthService _supabaseAuthService = Substitute.For<ISupabaseAuthService>();
    private readonly IRepository<User, UserId> _userRepository = Substitute.For<IRepository<User, UserId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private AcceptInviteHandler CreateHandler() => new(_supabaseAuthService, _userRepository, _unitOfWork, _auditService);

    private static User CreateInvitedUser(Guid userId, Guid tenantId) =>
        User.Invite(UserId.From(userId), tenantId, "Convidado", Email.Create("convidado@teste.com").Value, UserRole.Lawyer).Value;

    [Fact]
    public async Task Handle_WithValidInviteToken_ActivatesUserAndSetsPassword()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var invitedUser = CreateInvitedUser(userId, tenantId);

        _supabaseAuthService.GetUserFromAccessTokenAsync("invite-token", Arg.Any<CancellationToken>())
            .Returns(Result.Success(new SupabaseUserInfo(userId, "convidado@teste.com", true)));
        _userRepository.GetByIdAsync(UserId.From(userId), Arg.Any<CancellationToken>()).Returns(invitedUser);
        _supabaseAuthService.SetPasswordAsync("invite-token", "NovaSenha123!", Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await CreateHandler().Handle(
            new AcceptInviteCommand("invite-token", "NovaSenha123!", "Nome Confirmado"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.TenantId.Should().Be(tenantId);
        invitedUser.IsActive.Should().BeTrue();
        invitedUser.Name.Should().Be("Nome Confirmado");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyActive_ReturnsConflictWithoutSettingPassword()
    {
        var userId = Guid.NewGuid();
        var activeUser = CreateInvitedUser(userId, Guid.NewGuid());
        activeUser.Activate();

        _supabaseAuthService.GetUserFromAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new SupabaseUserInfo(userId, "convidado@teste.com", true)));
        _userRepository.GetByIdAsync(UserId.From(userId), Arg.Any<CancellationToken>()).Returns(activeUser);

        var result = await CreateHandler().Handle(
            new AcceptInviteCommand("invite-token", "NovaSenha123!", "Nome"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_ALREADY_ACTIVE));
        await _supabaseAuthService.DidNotReceiveWithAnyArgs()
            .SetPasswordAsync(default!, default!, cancellationToken: default);
    }

    [Fact]
    public async Task Handle_WhenLocalUserNotFound_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        _supabaseAuthService.GetUserFromAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new SupabaseUserInfo(userId, "convidado@teste.com", true)));
        _userRepository.GetByIdAsync(UserId.From(userId), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(
            new AcceptInviteCommand("invite-token", "NovaSenha123!", "Nome"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.USER_NOT_FOUND));
    }

    [Fact]
    public async Task Handle_WhenAccessTokenIsInvalid_ReturnsFailureWithoutTouchingRepository()
    {
        var error = Error.Unauthorized("auth.invalid_token", "Token de convite inválido.");
        _supabaseAuthService.GetUserFromAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<SupabaseUserInfo>(error));

        var result = await CreateHandler().Handle(
            new AcceptInviteCommand("token-invalido", "NovaSenha123!", "Nome"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, cancellationToken: default);
    }
}
