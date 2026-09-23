using Advocacia.Platform.Domain.FeatureFlags;
using FluentAssertions;
using Advocacia.BuildingBlocks.Domain.Errors;

namespace Advocacia.Platform.Domain.UnitTests.FeatureFlags;

public class FeatureFlagTests
{
    private static FeatureFlagKey ValidKey => FeatureFlagKey.Create("athena.enabled").Value;

    [Fact]
    public void Create_WhenValid_Succeeds()
    {
        var now = DateTimeOffset.UtcNow;

        var result = FeatureFlag.Create(Guid.NewGuid(), ValidKey, isEnabled: false, now);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeFalse();
        result.Value.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Create_WhenTenantEmpty_Fails()
    {
        var result = FeatureFlag.Create(Guid.Empty, ValidKey, isEnabled: false, DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.COMMON_INVALID_OPERATION));
    }

    [Fact]
    public void Enable_WhenDisabled_Succeeds()
    {
        var flag = FeatureFlag.Create(Guid.NewGuid(), ValidKey, isEnabled: false, DateTimeOffset.UtcNow.AddDays(-1)).Value;
        var now = DateTimeOffset.UtcNow;

        var result = flag.Enable(now);

        result.IsSuccess.Should().BeTrue();
        flag.IsEnabled.Should().BeTrue();
        flag.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_Fails()
    {
        var flag = FeatureFlag.Create(Guid.NewGuid(), ValidKey, isEnabled: true, DateTimeOffset.UtcNow).Value;

        var result = flag.Enable(DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.COMMON_INVALID_OPERATION));
    }

    [Fact]
    public void Disable_WhenEnabled_Succeeds()
    {
        var flag = FeatureFlag.Create(Guid.NewGuid(), ValidKey, isEnabled: true, DateTimeOffset.UtcNow.AddDays(-1)).Value;
        var now = DateTimeOffset.UtcNow;

        var result = flag.Disable(now);

        result.IsSuccess.Should().BeTrue();
        flag.IsEnabled.Should().BeFalse();
        flag.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_Fails()
    {
        var flag = FeatureFlag.Create(Guid.NewGuid(), ValidKey, isEnabled: false, DateTimeOffset.UtcNow).Value;

        var result = flag.Disable(DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.COMMON_INVALID_OPERATION));
    }
}
