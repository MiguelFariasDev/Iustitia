using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Auth.RefreshToken;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Auth;

public class RefreshTokenHandlerTests
{
    private readonly ISupabaseAuthService _supabaseAuthService = Substitute.For<ISupabaseAuthService>();

    private RefreshTokenHandler CreateHandler() => new(_supabaseAuthService);

    [Fact]
    public async Task Handle_WithValidRefreshToken_ReturnsNewSession()
    {
        var session = new SupabaseSession(Guid.NewGuid(), "fulano@teste.com", "new-access", "new-refresh", 3600);
        _supabaseAuthService.RefreshTokenAsync("old-refresh", Arg.Any<CancellationToken>()).Returns(Result.Success(session));

        var result = await CreateHandler().Handle(new RefreshTokenCommand("old-refresh"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access");
        result.Value.RefreshToken.Should().Be("new-refresh");
        result.Value.ExpiresInSeconds.Should().Be(3600);
    }

    [Fact]
    public async Task Handle_WithInvalidOrExpiredRefreshToken_ReturnsFailure()
    {
        var error = Error.Unauthorized("auth.invalid_refresh_token", "Refresh token inválido ou expirado.");
        _supabaseAuthService.RefreshTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<SupabaseSession>(error));

        var result = await CreateHandler().Handle(new RefreshTokenCommand("expirado"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }
}
