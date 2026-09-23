using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Features.Settings.UpdateSettings;
using Advocacia.Platform.Domain.Settings;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Settings;

public class UpdateSettingsHandlerTests
{
    private readonly IRepository<Setting, SettingId> _settingRepository = Substitute.For<IRepository<Setting, SettingId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly Guid _tenantId = Guid.NewGuid();

    public UpdateSettingsHandlerTests()
    {
        _currentUser.TenantId.Returns(_tenantId);
        _dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
    }

    private UpdateSettingsHandler CreateHandler() => new(_settingRepository, _unitOfWork, _currentUser, _dateTimeProvider);

    [Fact]
    public async Task Handle_WhenKeyDoesNotExist_CreatesNewSetting()
    {
        _settingRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Setting>>(), Arg.Any<CancellationToken>()).Returns((Setting?)null);

        var result = await CreateHandler().Handle(new UpdateSettingsCommand("dashboard.layout", "{\"a\":1}"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Key.Should().Be("dashboard.layout");
        _settingRepository.Received(1).Add(Arg.Any<Setting>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenKeyAlreadyExists_UpdatesValueWithoutAdding()
    {
        var existing = Setting.Create(_tenantId, SettingKey.Create("dashboard.layout").Value, "{\"a\":1}", DateTimeOffset.UtcNow).Value;
        _settingRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Setting>>(), Arg.Any<CancellationToken>()).Returns(existing);

        var result = await CreateHandler().Handle(new UpdateSettingsCommand("dashboard.layout", "{\"a\":2}"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ValueJson.Should().Be("{\"a\":2}");
        existing.ValueJson.Should().Be("{\"a\":2}");
        _settingRepository.DidNotReceiveWithAnyArgs().Add(default!);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidKey_ReturnsValidationFailure()
    {
        var result = await CreateHandler().Handle(new UpdateSettingsCommand("Chave Inválida!", "{}"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.SETTINGS_KEY_INVALID));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutTenant_ReturnsFailure()
    {
        _currentUser.TenantId.Returns((Guid?)null);

        var result = await CreateHandler().Handle(new UpdateSettingsCommand("dashboard.layout", "{}"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
    }
}
