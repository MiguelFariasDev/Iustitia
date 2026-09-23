using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Platform.Application.Features.FeatureFlags.UpdateFeatureFlag;
using Advocacia.Platform.Domain.FeatureFlags;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Platform.Application.UnitTests.Features.FeatureFlags;

public class UpdateFeatureFlagHandlerTests
{
    private readonly IRepository<FeatureFlag, FeatureFlagId> _featureFlagRepository = Substitute.For<IRepository<FeatureFlag, FeatureFlagId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly Guid _tenantId = Guid.NewGuid();

    public UpdateFeatureFlagHandlerTests()
    {
        _currentUser.TenantId.Returns(_tenantId);
        _dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
    }

    private UpdateFeatureFlagHandler CreateHandler() => new(_featureFlagRepository, _unitOfWork, _currentUser, _dateTimeProvider);

    [Fact]
    public async Task Handle_WhenFlagDoesNotExist_CreatesIt()
    {
        _featureFlagRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<FeatureFlag>>(), Arg.Any<CancellationToken>())
            .Returns((FeatureFlag?)null);

        var result = await CreateHandler().Handle(new UpdateFeatureFlagCommand("athena.enabled", true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
        _featureFlagRepository.Received(1).Add(Arg.Any<FeatureFlag>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFlagExistsWithDifferentState_TransitionsAndSaves()
    {
        var flag = FeatureFlag.Create(_tenantId, FeatureFlagKey.Create("athena.enabled").Value, false, DateTimeOffset.UtcNow).Value;
        _featureFlagRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<FeatureFlag>>(), Arg.Any<CancellationToken>()).Returns(flag);

        var result = await CreateHandler().Handle(new UpdateFeatureFlagCommand("athena.enabled", true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        flag.IsEnabled.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFlagAlreadyInDesiredState_IsIdempotentAndDoesNotSave()
    {
        var flag = FeatureFlag.Create(_tenantId, FeatureFlagKey.Create("athena.enabled").Value, true, DateTimeOffset.UtcNow).Value;
        _featureFlagRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<FeatureFlag>>(), Arg.Any<CancellationToken>()).Returns(flag);

        var result = await CreateHandler().Handle(new UpdateFeatureFlagCommand("athena.enabled", true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidKey_ReturnsValidationFailure()
    {
        var result = await CreateHandler().Handle(new UpdateFeatureFlagCommand("Chave Inválida!", true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.FEATURE_FLAG_KEY_INVALID));
    }
}
