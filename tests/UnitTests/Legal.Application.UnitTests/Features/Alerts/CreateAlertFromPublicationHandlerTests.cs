using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.Alerts;
using Advocacia.Modules.Legal.Domain.Publications;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.Alerts;

public class CreateAlertFromPublicationHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IPublicationRepository _repository = Substitute.For<IPublicationRepository>();
    private readonly IAlertPublisher _publisher = Substitute.For<IAlertPublisher>();
    private readonly CreateAlertFromPublicationHandler _handler;

    public CreateAlertFromPublicationHandlerTests() => _handler = new(_repository, _publisher);

    [Fact]
    public async Task Handle_PublishesAPublicationAlertForTheUser()
    {
        var userId = Guid.NewGuid();
        var publication = TestPublications.Create(_tenantId, content: "TEXTO-SIGILOSO-DO-PROCESSO");
        _repository.GetByIdAsync(publication.Id, Arg.Any<CancellationToken>()).Returns(publication);
        _publisher.PublishAsync(Arg.Any<AlertRequest>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(
            new CreateAlertFromPublicationCommand(publication.Id.Value, userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().BeTrue();
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AlertRequest>(r =>
                r.TenantId == _tenantId && r.UserId == userId && r.Type == "Publication"
                && r.ReferenceId == publication.Id.Value
                && r.Description!.Contains(TestPublications.ValidCnjNumber)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NeverCopiesTheRawContentIntoTheAlert()
    {
        var publication = TestPublications.Create(_tenantId, content: "TEXTO-SIGILOSO-DO-PROCESSO");
        _repository.GetByIdAsync(publication.Id, Arg.Any<CancellationToken>()).Returns(publication);

        await _handler.Handle(new CreateAlertFromPublicationCommand(publication.Id.Value, Guid.NewGuid()), CancellationToken.None);

        await _publisher.DidNotReceive().PublishAsync(
            Arg.Is<AlertRequest>(r => r.Title.Contains("SIGILOSO") || r.Description!.Contains("SIGILOSO")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTheAlertAlreadyExists_ReportsNotCreated()
    {
        var publication = TestPublications.Create(_tenantId);
        _repository.GetByIdAsync(publication.Id, Arg.Any<CancellationToken>()).Returns(publication);
        _publisher.PublishAsync(Arg.Any<AlertRequest>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(
            new CreateAlertFromPublicationCommand(publication.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithUnknownPublication_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<PublicationId>(), Arg.Any<CancellationToken>()).Returns((Publication?)null);

        var result = await _handler.Handle(
            new CreateAlertFromPublicationCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_NOT_FOUND));
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
    }

    [Fact]
    public void Validator_RejectsEmptyIds()
    {
        new CreateAlertFromPublicationValidator()
            .Validate(new CreateAlertFromPublicationCommand(Guid.Empty, Guid.Empty))
            .IsValid.Should().BeFalse();
    }
}
