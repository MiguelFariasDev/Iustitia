using Advocacia.BuildingBlocks.Infrastructure.Caching;
using Advocacia.Platform.Domain.FeatureFlags;
using Advocacia.Platform.Infrastructure.FeatureFlags;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Infrastructure.UnitTests.FeatureFlags;

public sealed class FeatureFlagServiceTests
{
    private static ICacheService CreatePassthroughCache()
    {
        var cache = Substitute.For<ICacheService>();
        cache.GetOrSetAsync(
                Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<bool>>>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.ArgAt<Func<CancellationToken, Task<bool>>>(1)(CancellationToken.None));

        return cache;
    }

    [Fact]
    public async Task IsEnabledAsync_WhenFlagExistsAndEnabled_ReturnsTrue()
    {
        using var dbContext = TestPlatformDbContext.Create();
        var tenantId = Guid.NewGuid();
        var key = FeatureFlagKey.Create(FeatureFlagKeys.AthenaAssistant).Value;
        dbContext.Set<FeatureFlag>().Add(FeatureFlag.Create(tenantId, key, isEnabled: true, DateTimeOffset.UtcNow).Value);
        await dbContext.SaveChangesAsync();

        var sut = new FeatureFlagService(dbContext, CreatePassthroughCache());

        (await sut.IsEnabledAsync(FeatureFlagKeys.AthenaAssistant, tenantId)).Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_WhenFlagDoesNotExist_ReturnsFalse_FailClosed()
    {
        using var dbContext = TestPlatformDbContext.Create();

        var sut = new FeatureFlagService(dbContext, CreatePassthroughCache());

        (await sut.IsEnabledAsync(FeatureFlagKeys.AthenaAssistant, Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_IsScopedPerTenant()
    {
        using var dbContext = TestPlatformDbContext.Create();
        var enabledTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var key = FeatureFlagKey.Create(FeatureFlagKeys.KanbanBoard).Value;
        dbContext.Set<FeatureFlag>().Add(FeatureFlag.Create(enabledTenant, key, isEnabled: true, DateTimeOffset.UtcNow).Value);
        await dbContext.SaveChangesAsync();

        var sut = new FeatureFlagService(dbContext, CreatePassthroughCache());

        (await sut.IsEnabledAsync(FeatureFlagKeys.KanbanBoard, otherTenant)).Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateCacheAsync_RemovesCacheEntry()
    {
        using var dbContext = TestPlatformDbContext.Create();
        var cache = Substitute.For<ICacheService>();
        var sut = new FeatureFlagService(dbContext, cache);
        var tenantId = Guid.NewGuid();

        await sut.InvalidateCacheAsync(FeatureFlagKeys.LegalModule, tenantId);

        await cache.Received(1).RemoveAsync($"featureflag:{tenantId}:{FeatureFlagKeys.LegalModule}", Arg.Any<CancellationToken>());
    }
}
