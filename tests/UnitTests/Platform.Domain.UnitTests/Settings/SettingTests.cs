using Advocacia.Platform.Domain.Settings;
using FluentAssertions;
using Advocacia.BuildingBlocks.Domain.Errors;

namespace Advocacia.Platform.Domain.UnitTests.Settings;

public class SettingTests
{
    private static SettingKey ValidKey => SettingKey.Create("dashboard.default_layout").Value;

    [Fact]
    public void Create_WhenValid_Succeeds()
    {
        var now = DateTimeOffset.UtcNow;

        var result = Setting.Create(Guid.NewGuid(), ValidKey, "{\"cards\":[]}", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ValueJson.Should().Be("{\"cards\":[]}");
        result.Value.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Create_WhenTenantEmpty_Fails()
    {
        var result = Setting.Create(Guid.Empty, ValidKey, "{}", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.COMMON_INVALID_OPERATION));
    }

    [Fact]
    public void UpdateValue_ChangesValueAndTimestamp()
    {
        var setting = Setting.Create(Guid.NewGuid(), ValidKey, "{}", DateTimeOffset.UtcNow.AddDays(-1)).Value;
        var now = DateTimeOffset.UtcNow;

        var result = setting.UpdateValue("{\"cards\":[\"tasks\"]}", now);

        result.IsSuccess.Should().BeTrue();
        setting.ValueJson.Should().Be("{\"cards\":[\"tasks\"]}");
        setting.UpdatedAt.Should().Be(now);
    }
}
