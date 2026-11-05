using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Platform.Domain.Devices;
using FluentAssertions;

namespace Advocacia.Platform.Domain.UnitTests.Devices;

public class DeviceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private const string Token = "fcm-token-de-teste-com-mais-de-vinte-caracteres";

    private static Device NewDevice() => Device.Register(TenantId, UserId, DevicePlatform.Android, Token, Now).Value;

    [Fact]
    public void Register_WithValidData_CreatesAnActiveDevice()
    {
        var result = Device.Register(TenantId, UserId, DevicePlatform.Ios, "  " + Token + "  ", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsRevoked.Should().BeFalse();
        result.Value.DeviceToken.Should().Be(Token); // sem os espaços das pontas
        result.Value.LastSeenAt.Should().Be(Now);
        result.Value.CreatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curto")]
    [InlineData("token com espaço no meio mas comprido o bastante")]
    public void Register_WithInvalidToken_Fails(string? token)
    {
        var result = Device.Register(TenantId, UserId, DevicePlatform.Android, token, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.DEVICE_TOKEN_INVALID));
    }

    [Fact]
    public void Register_WithAnOversizedToken_Fails()
    {
        Device.Register(TenantId, UserId, DevicePlatform.Android, new string('a', Device.TokenMaxLength + 1), Now)
            .IsFailure.Should().BeTrue();
        Device.Register(TenantId, UserId, DevicePlatform.Android, new string('a', Device.TokenMaxLength), Now)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Register_WithoutTenantOrUser_Fails()
    {
        Device.Register(Guid.Empty, UserId, DevicePlatform.Ios, Token, Now).IsFailure.Should().BeTrue();
        Device.Register(TenantId, Guid.Empty, DevicePlatform.Ios, Token, Now).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("iOS", DevicePlatform.Ios)]
    [InlineData("ios", DevicePlatform.Ios)]
    [InlineData(" Android ", DevicePlatform.Android)]
    [InlineData("ANDROID", DevicePlatform.Android)]
    public void TryParsePlatform_AcceptsBothPlatformsIgnoringCase(string value, DevicePlatform expected)
    {
        Device.TryParsePlatform(value, out var platform).Should().BeTrue();
        platform.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("windows")]
    [InlineData("2")] // um número que é um valor válido do enum não vale: só o NOME da plataforma
    [InlineData("99")]
    public void TryParsePlatform_RejectsAnythingElse(string? value)
    {
        Device.TryParsePlatform(value, out _).Should().BeFalse();
    }

    [Fact]
    public void Refresh_UpdatesOwnerPlatformAndLastSeen()
    {
        var device = NewDevice();
        var otherUser = Guid.NewGuid();
        var later = Now.AddHours(3);

        var result = device.Refresh(otherUser, DevicePlatform.Ios, later);

        result.IsSuccess.Should().BeTrue();
        device.UserId.Should().Be(otherUser); // aparelho compartilhado trocou de dono
        device.Platform.Should().Be(DevicePlatform.Ios);
        device.LastSeenAt.Should().Be(later);
        device.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Revoke_MarksTheDeviceRevoked()
    {
        var device = NewDevice();

        var result = device.Revoke(Now.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        device.IsRevoked.Should().BeTrue();
        device.RevokedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void Revoke_Twice_FailsWithoutChangingTheOriginalRevocationTime()
    {
        var device = NewDevice();
        device.Revoke(Now);

        var second = device.Revoke(Now.AddHours(1));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be(nameof(ErrorCode.DEVICE_ALREADY_REVOKED));
        device.RevokedAt.Should().Be(Now);
    }

    [Fact]
    public void Refresh_OnARevokedDevice_IsRefusedAndChangesNothing()
    {
        // A revogação é PERMANENTE: se re-registrar reativasse, quem roubou o aparelho (e ainda tem a
        // sessão) desfaria a revogação só reabrindo o app.
        var device = NewDevice();
        device.Revoke(Now);
        var thief = Guid.NewGuid();

        var result = device.Refresh(thief, DevicePlatform.Ios, Now.AddDays(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.DEVICE_REVOKED));
        device.IsRevoked.Should().BeTrue();
        device.UserId.Should().Be(UserId);
        device.Platform.Should().Be(DevicePlatform.Android);
        device.LastSeenAt.Should().Be(Now);
    }

    [Fact]
    public void ToString_NeverIncludesTheDeliveryToken()
    {
        var device = NewDevice();

        device.ToString().Should().NotContain(Token);
        $"{device}".Should().NotContain(Token);
    }
}
