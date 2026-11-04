using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.CapturePublication;
using Advocacia.Modules.Legal.Domain.Events;
using Advocacia.Modules.Legal.Domain.Publications;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.CapturePublication;

public class CapturePublicationHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IPublicationRepository _repository = Substitute.For<IPublicationRepository>();
    private readonly ILegalUnitOfWork _unitOfWork = Substitute.For<ILegalUnitOfWork>();
    private readonly CapturePublicationHandler _handler;

    public CapturePublicationHandlerTests()
    {
        _handler = new CapturePublicationHandler(
            _repository,
            _unitOfWork,
            new TestPublications.FixedDateTimeProvider(),
            NullLogger<CapturePublicationHandler>.Instance);
    }

    private CapturePublicationCommand BuildCommand(
        string externalId = "cnj-1",
        string cnjNumber = TestPublications.ValidCnjNumber,
        string content = "Intimação para manifestação no prazo legal.",
        DateTimeOffset? publishedAt = null) =>
        new(
            _tenantId,
            externalId,
            cnjNumber,
            PublicationSource.DJEN,
            content,
            publishedAt ?? TestPublications.Now.AddDays(-1));

    [Fact]
    public async Task Handle_WithNewPublication_PersistsAndReportsItAsNew()
    {
        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Existing.Should().BeFalse();
        result.Value.PublicationId.Should().NotBeEmpty();

        _repository.Received(1).Add(Arg.Is<Publication>(publication =>
            publication.TenantId == _tenantId
            && publication.ExternalId == "cnj-1"
            && publication.Status == PublicationStatus.PendingReview));

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNewPublication_QueuesThePublicationCapturedEvent()
    {
        Publication? added = null;
        _repository.When(repository => repository.Add(Arg.Any<Publication>()))
            .Do(call => added = call.Arg<Publication>());

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        // O evento fica pendente no agregado; o OutboxInterceptor o grava na MESMA transação
        // do SaveChanges (ver ADR-008).
        added!.DomainEvents.OfType<PublicationCapturedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WithDuplicatedExternalId_SucceedsAsExisting_WithoutPersisting()
    {
        // Idempotência: a janela de busca do job é deliberadamente sobreposta, então
        // recapturar é o caso comum e não pode ser erro (ver ADR-056).
        var existing = TestPublications.Create(_tenantId, externalId: "cnj-1");
        _repository
            .GetByExternalIdAsync(_tenantId, "cnj-1", PublicationSource.DJEN, Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _handler.Handle(BuildCommand(externalId: "cnj-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Existing.Should().BeTrue();
        result.Value.PublicationId.Should().Be(existing.Id.Value);

        _repository.DidNotReceive().Add(Arg.Any<Publication>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeduplicatesPerSource()
    {
        // Uma publicação com o mesmo id numérico vinda de outra origem não é a mesma
        // publicação — por isso Source entra na chave de deduplicação.
        _repository
            .GetByExternalIdAsync(_tenantId, "cnj-1", PublicationSource.DJEN, Arg.Any<CancellationToken>())
            .Returns((Publication?)null);

        var result = await _handler.Handle(BuildCommand(externalId: "cnj-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByExternalIdAsync(
            _tenantId, "cnj-1", PublicationSource.DJEN, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidCnjNumber_FailsWithoutPersisting()
    {
        var result = await _handler.Handle(BuildCommand(cnjNumber: "0000000-00.0000.0.00.0000"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_CNJ));
        _repository.DidNotReceive().Add(Arg.Any<Publication>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithWrongCheckDigits_Fails()
    {
        var result = await _handler.Handle(BuildCommand(cnjNumber: "1234567-49.2026.8.26.0001"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_CNJ));
    }

    [Fact]
    public async Task Handle_WithEmptyContent_Fails()
    {
        var result = await _handler.Handle(BuildCommand(content: "   "), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_EMPTY_CONTENT));
    }

    [Fact]
    public async Task Handle_WithPublishedAtInTheFuture_Fails()
    {
        var result = await _handler.Handle(
            BuildCommand(publishedAt: TestPublications.Now.AddDays(1)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_DATE));
    }

    [Fact]
    public async Task Handle_NormalizesTheContentBeforePersisting()
    {
        Publication? added = null;
        _repository.When(repository => repository.Add(Arg.Any<Publication>()))
            .Do(call => added = call.Arg<Publication>());

        await _handler.Handle(BuildCommand(content: "<p>Intimação  para\n manifestação.</p>"), CancellationToken.None);

        added!.RawContent.Value.Should().Be("Intimação para manifestação.");
    }

    [Fact]
    public async Task Handle_StampsCapturedAtFromTheClockProvider()
    {
        Publication? added = null;
        _repository.When(repository => repository.Add(Arg.Any<Publication>()))
            .Do(call => added = call.Arg<Publication>());

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        added!.CapturedAt.Should().Be(TestPublications.Now);
    }
}
