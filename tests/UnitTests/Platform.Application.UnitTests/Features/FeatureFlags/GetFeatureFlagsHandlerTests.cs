using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.Platform.Application.Features.FeatureFlags.GetFeatureFlags;
using Advocacia.Platform.Domain.FeatureFlags;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.FeatureFlags;

public class GetFeatureFlagsHandlerTests
{
    private readonly IRepository<FeatureFlag, FeatureFlagId> _featureFlagRepository = Substitute.For<IRepository<FeatureFlag, FeatureFlagId>>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private GetFeatureFlagsHandler CreateHandler() => new(_featureFlagRepository, _currentUser, TestMapper.Create());

    [Fact]
    public async Task Handle_WithTenant_ReturnsMappedFlags()
    {
        _currentUser.TenantId.Returns(Guid.NewGuid());
        var flag = FeatureFlag.Create(Guid.NewGuid(), FeatureFlagKey.Create("athena.enabled").Value, true, DateTimeOffset.UtcNow).Value;
        _featureFlagRepository.ListAsync(Arg.Any<ISpecification<FeatureFlag>>(), Arg.Any<CancellationToken>()).Returns([flag]);

        var result = await CreateHandler().Handle(new GetFeatureFlagsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.Key == "athena.enabled" && i.IsEnabled);
    }

    [Fact]
    public async Task Handle_WithoutTenant_ReturnsFailureWithoutQuerying()
    {
        _currentUser.TenantId.Returns((Guid?)null);

        var result = await CreateHandler().Handle(new GetFeatureFlagsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.AUTHORIZATION_TENANT_MISMATCH));
        await _featureFlagRepository.DidNotReceiveWithAnyArgs().ListAsync(default!, cancellationToken: default);
    }
}
