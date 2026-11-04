using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.MarkPublicationAsRead;
using Advocacia.Modules.Legal.Domain.Publications;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.MarkPublicationAsRead;

public class MarkPublicationAsReadHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IPublicationRepository _repository = Substitute.For<IPublicationRepository>();
    private readonly ILegalUnitOfWork _unitOfWork = Substitute.For<ILegalUnitOfWork>();
    private readonly MarkPublicationAsReadHandler _handler;

    public MarkPublicationAsReadHandlerTests()
    {
        _handler = new MarkPublicationAsReadHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WithUnreadPublication_MarksAndPersists()
    {
        var publication = TestPublications.Create(_tenantId);
        _repository.GetByIdAsync(publication.Id, Arg.Any<CancellationToken>()).Returns(publication);

        var result = await _handler.Handle(new MarkPublicationAsReadCommand(publication.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        publication.IsRead.Should().BeTrue();
        _repository.Received(1).Update(publication);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DoesNotChangeTheReviewStatus()
    {
        var publication = TestPublications.Create(_tenantId);
        _repository.GetByIdAsync(publication.Id, Arg.Any<CancellationToken>()).Returns(publication);

        await _handler.Handle(new MarkPublicationAsReadCommand(publication.Id.Value), CancellationToken.None);

        publication.Status.Should().Be(PublicationStatus.PendingReview);
    }

    [Fact]
    public async Task Handle_WithUnknownId_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<PublicationId>(), Arg.Any<CancellationToken>()).Returns((Publication?)null);

        var result = await _handler.Handle(new MarkPublicationAsReadCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_NOT_FOUND));
    }

    [Fact]
    public async Task Handle_WithAlreadyReadPublication_FailsWithoutPersisting()
    {
        var publication = TestPublications.Create(_tenantId);
        publication.MarkAsRead();
        _repository.GetByIdAsync(publication.Id, Arg.Any<CancellationToken>()).Returns(publication);

        var result = await _handler.Handle(new MarkPublicationAsReadCommand(publication.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_ALREADY_READ));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
