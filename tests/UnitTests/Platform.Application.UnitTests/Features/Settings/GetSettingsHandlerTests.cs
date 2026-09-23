using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Application.Features.Settings.GetSettings;
using Advocacia.Platform.Domain.Settings;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.Settings;

public class GetSettingsHandlerTests
{
    private readonly IRepository<Setting, SettingId> _settingRepository = Substitute.For<IRepository<Setting, SettingId>>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private GetSettingsHandler CreateHandler() => new(_settingRepository, _currentUser, TestMapper.Create());

    [Fact]
    public async Task Handle_WithTenant_ReturnsMappedSettings()
    {
        _currentUser.TenantId.Returns(Guid.NewGuid());
        var setting = Setting.Create(Guid.NewGuid(), SettingKey.Create("dashboard.layout").Value, "{\"a\":1}", DateTimeOffset.UtcNow).Value;
        _settingRepository.ListAsync(Arg.Any<ISpecification<Setting>>(), Arg.Any<CancellationToken>()).Returns([setting]);

        var result = await CreateHandler().Handle(new GetSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.Key == "dashboard.layout" && i.ValueJson == "{\"a\":1}");
    }

    [Fact]
    public async Task Handle_WithoutTenant_ReturnsFailureWithoutQuerying()
    {
        _currentUser.TenantId.Returns((Guid?)null);

        var result = await CreateHandler().Handle(new GetSettingsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        await _settingRepository.DidNotReceiveWithAnyArgs().ListAsync(default!, cancellationToken: default);
    }
}
