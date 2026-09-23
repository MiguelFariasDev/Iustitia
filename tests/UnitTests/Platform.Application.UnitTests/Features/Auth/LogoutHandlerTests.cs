using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Auth.Logout;
using Advocacia.Platform.Domain.Auditing;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Auth;

public class LogoutHandlerTests
{
    private readonly ISupabaseAuthService _supabaseAuthService = Substitute.For<ISupabaseAuthService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private LogoutHandler CreateHandler() => new(_supabaseAuthService, _auditService, _currentUser);

    [Fact]
    public async Task Handle_WithAuthenticatedUser_SignsOutAndAudits()
    {
        var userId = Guid.NewGuid();
        _supabaseAuthService.SignOutAsync("access-token", Arg.Any<CancellationToken>()).Returns(Result.Success());
        _currentUser.UserId.Returns(userId);

        var result = await CreateHandler().Handle(new LogoutCommand("access-token"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _auditService.Received(1).RecordAsync(
            AuditAction.Logout, "User", userId.ToString(),
            before: null, after: null, performedByUserId: null, tenantId: null,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutCurrentUser_SignsOutWithoutAuditing()
    {
        _supabaseAuthService.SignOutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _currentUser.UserId.Returns((Guid?)null);

        var result = await CreateHandler().Handle(new LogoutCommand("access-token"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _auditService.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default!, cancellationToken: default);
    }

    [Fact]
    public async Task Handle_WhenSupabaseSignOutFails_ReturnsFailureWithoutAuditing()
    {
        var error = Error.Unauthorized("auth.invalid_token", "Token inválido.");
        _supabaseAuthService.SignOutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Failure(error));

        var result = await CreateHandler().Handle(new LogoutCommand("token-invalido"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        await _auditService.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default!, cancellationToken: default);
    }
}
