using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Abstractions;
using Advocacia.Platform.Application.Features.Devices;
using Advocacia.Platform.Domain.Devices;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Devices;

public class DeviceHandlersTests
{
    private const string Token = "fcm-token-de-teste-com-mais-de-vinte-caracteres";

    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    public DeviceHandlersTests()
    {
        _currentUser.TenantId.Returns(_tenantId);
        _currentUser.UserId.Returns(_userId);
        _clock.UtcNow.Returns(_now);
    }

    private Device ExistingDevice(Guid? owner = null) =>
        Device.Register(_tenantId, owner ?? _userId, DevicePlatform.Android, Token, _now.AddDays(-1)).Value;

    // --- Registro ------------------------------------------------------------------------

    [Fact]
    public async Task Register_WithANewToken_CreatesTheDeviceAndSaves()
    {
        _devices.GetByTokenAsync(_tenantId, Token, Arg.Any<CancellationToken>()).Returns((Device?)null);
        var handler = new RegisterDeviceHandler(_devices, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new RegisterDeviceCommand(Token, "Android"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Platform.Should().Be("Android");
        result.Value.DeviceId.Should().NotBeEmpty();
        _devices.Received(1).Add(Arg.Is<Device>(d =>
            d.TenantId == _tenantId && d.UserId == _userId && d.DeviceToken == Token && !d.IsRevoked));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_WithAnExistingToken_RefreshesInsteadOfDuplicating()
    {
        var existing = ExistingDevice();
        _devices.GetByTokenAsync(_tenantId, Token, Arg.Any<CancellationToken>()).Returns(existing);
        var handler = new RegisterDeviceHandler(_devices, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new RegisterDeviceCommand(Token, "iOS"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeviceId.Should().Be(existing.Id.Value);
        result.Value.LastSeenAt.Should().Be(_now);
        existing.Platform.Should().Be(DevicePlatform.Ios);
        _devices.DidNotReceiveWithAnyArgs().Add(default!);
        _devices.Received(1).Update(existing);
    }

    [Fact]
    public async Task Register_OfASharedDevice_MovesItToTheNewUser()
    {
        var colleague = Guid.NewGuid();
        var existing = ExistingDevice(owner: colleague);
        _devices.GetByTokenAsync(_tenantId, Token, Arg.Any<CancellationToken>()).Returns(existing);
        var handler = new RegisterDeviceHandler(_devices, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new RegisterDeviceCommand(Token, "Android"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task Register_OfARevokedToken_FailsWithDeviceRevokedAndDoesNotSave()
    {
        var revoked = ExistingDevice();
        revoked.Revoke(_now.AddHours(-1));
        _devices.GetByTokenAsync(_tenantId, Token, Arg.Any<CancellationToken>()).Returns(revoked);
        var handler = new RegisterDeviceHandler(_devices, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new RegisterDeviceCommand(Token, "Android"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.DEVICE_REVOKED));
        revoked.IsRevoked.Should().BeTrue();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Register_WithAnInvalidPlatform_Fails()
    {
        var handler = new RegisterDeviceHandler(_devices, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new RegisterDeviceCommand(Token, "windows"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.DEVICE_PLATFORM_INVALID));
    }

    [Fact]
    public async Task Register_WithoutATenantOrUser_IsRefused()
    {
        _currentUser.TenantId.Returns((Guid?)null);
        var handler = new RegisterDeviceHandler(_devices, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new RegisterDeviceCommand(Token, "Android"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
    }

    [Fact]
    public async Task Register_ResponseNeverCarriesTheToken()
    {
        _devices.GetByTokenAsync(_tenantId, Token, Arg.Any<CancellationToken>()).Returns((Device?)null);
        var handler = new RegisterDeviceHandler(_devices, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new RegisterDeviceCommand(Token, "Android"), CancellationToken.None);

        System.Text.Json.JsonSerializer.Serialize(result.Value).Should().NotContain(Token);
    }

    // --- Validação ----------------------------------------------------------------------

    [Theory]
    [InlineData(Token, "Android", true)]
    [InlineData(Token, "ios", true)]
    [InlineData("curto", "Android", false)]
    [InlineData("", "Android", false)]
    [InlineData(Token, "palm", false)]
    public void Validator_AcceptsOnlyAValidTokenAndPlatform(string token, string platform, bool valid)
    {
        new RegisterDeviceValidator().Validate(new RegisterDeviceCommand(token, platform)).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Validator_ErrorMessageDoesNotEchoTheToken()
    {
        var secret = "token com espaço que é inválido mas comprido o bastante";

        var result = new RegisterDeviceValidator().Validate(new RegisterDeviceCommand(secret, "Android"));

        result.Errors.Should().NotBeEmpty();
        result.Errors.Select(e => e.ErrorMessage).Should().NotContain(m => m.Contains("espaço"));
    }

    // --- Listagem -----------------------------------------------------------------------

    [Fact]
    public async Task List_ReturnsTheUsersDevicesAndMarksTheCurrentOne()
    {
        var current = ExistingDevice();
        var other = Device.Register(_tenantId, _userId, DevicePlatform.Ios, Token + "-outro", _now).Value;
        _devices.ListForUserAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns([other, current]);
        var handler = new ListDevicesHandler(_devices, _currentUser);

        var result = await handler.Handle(new ListDevicesQuery(current.Id.Value), CancellationToken.None);

        result.Value.Should().HaveCount(2);
        result.Value.Single(d => d.Id == current.Id.Value).IsCurrent.Should().BeTrue();
        result.Value.Single(d => d.Id == other.Id.Value).IsCurrent.Should().BeFalse();
        result.Value.Single(d => d.Id == other.Id.Value).Platform.Should().Be("iOS");
    }

    [Fact]
    public async Task List_NeverExposesTheDeliveryToken()
    {
        var device = ExistingDevice();
        _devices.ListForUserAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns([device]);
        var handler = new ListDevicesHandler(_devices, _currentUser);

        var result = await handler.Handle(new ListDevicesQuery(), CancellationToken.None);

        System.Text.Json.JsonSerializer.Serialize(result.Value).Should().NotContain(Token);
        typeof(DeviceResponse).GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("Token"));
    }

    // --- Revogação ----------------------------------------------------------------------

    [Fact]
    public async Task Revoke_RevokesTheUsersDeviceAndSaves()
    {
        var device = ExistingDevice();
        _devices.GetForUserAsync(_tenantId, _userId, device.Id, Arg.Any<CancellationToken>()).Returns(device);
        var handler = new RevokeDeviceHandler(_devices, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new RevokeDeviceCommand(device.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        device.IsRevoked.Should().BeTrue();
        device.RevokedAt.Should().Be(_now);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revoke_OfAnotherUsersDevice_IsNotFoundNotForbidden()
    {
        // O repositório filtra por usuário: o dispositivo do colega "não existe" para este usuário, e a
        // resposta não revela que o id existe.
        var someoneElses = ExistingDevice(owner: Guid.NewGuid());
        _devices.GetForUserAsync(_tenantId, _userId, someoneElses.Id, Arg.Any<CancellationToken>()).Returns((Device?)null);
        var handler = new RevokeDeviceHandler(_devices, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new RevokeDeviceCommand(someoneElses.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.DEVICE_NOT_FOUND));
        someoneElses.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Revoke_AlreadyRevoked_Fails()
    {
        var device = ExistingDevice();
        device.Revoke(_now.AddHours(-2));
        _devices.GetForUserAsync(_tenantId, _userId, device.Id, Arg.Any<CancellationToken>()).Returns(device);
        var handler = new RevokeDeviceHandler(_devices, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new RevokeDeviceCommand(device.Id.Value), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.DEVICE_ALREADY_REVOKED));
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }
}
