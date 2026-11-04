using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.ChangePublicationStatus;
using Advocacia.Modules.Legal.Domain.Publications;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.ChangePublicationStatus;

public class ChangePublicationStatusHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IPublicationRepository _repository = Substitute.For<IPublicationRepository>();
    private readonly ILegalUnitOfWork _unitOfWork = Substitute.For<ILegalUnitOfWork>();
    private readonly ChangePublicationStatusHandler _handler;

    public ChangePublicationStatusHandlerTests() => _handler = new(_repository, _unitOfWork);

    [Fact]
    public async Task Handle_FromPendingReviewToApproved_PersistsTheNewStatus()
    {
        var publication = TestPublications.Create(_tenantId);
        _repository.GetByIdAsync(publication.Id, Arg.Any<CancellationToken>()).Returns(publication);

        var result = await _handler.Handle(
            new ChangePublicationStatusCommand(publication.Id.Value, "approved", "Prazo conferido"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        publication.Status.Should().Be(PublicationStatus.Approved);
        publication.ReviewNotes.Should().Be("Prazo conferido");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUnknownPublication_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<PublicationId>(), Arg.Any<CancellationToken>()).Returns((Publication?)null);

        var result = await _handler.Handle(
            new ChangePublicationStatusCommand(Guid.NewGuid(), "Approved"), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_NOT_FOUND));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BackToPendingReview_IsRejectedAndNothingIsSaved()
    {
        var publication = TestPublications.Create(_tenantId);
        publication.ChangeStatus(PublicationStatus.Approved);
        _repository.GetByIdAsync(publication.Id, Arg.Any<CancellationToken>()).Returns(publication);

        var result = await _handler.Handle(
            new ChangePublicationStatusCommand(publication.Id.Value, "PendingReview"), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_STATUS_TRANSITION));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("nao-existe")]
    [InlineData("")]
    public void Validator_RejectsUnknownOrEmptyStatus(string status)
    {
        var result = new ChangePublicationStatusValidator().Validate(
            new ChangePublicationStatusCommand(Guid.NewGuid(), status));

        result.IsValid.Should().BeFalse();
    }
}
