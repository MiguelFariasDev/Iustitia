using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Domain.Events;
using Advocacia.Modules.Legal.Domain.Publications;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Publications;

public class PublicationTests
{
    private static readonly DateTimeOffset Now = TestPublications.Now;

    [Fact]
    public void Create_WithValidData_StartsPendingReviewAndUnread()
    {
        var tenantId = Guid.NewGuid();

        var result = Publication.Create(
            tenantId,
            TestPublications.CnjNumber(),
            "cnj-42",
            PublicationSource.DJEN,
            TestPublications.RawContent(),
            TestPublications.PublishedAt(),
            capturedAt: Now);

        result.IsSuccess.Should().BeTrue();

        var publication = result.Value;
        publication.TenantId.Should().Be(tenantId);
        publication.ExternalId.Should().Be("cnj-42");
        publication.Source.Should().Be(PublicationSource.DJEN);
        publication.CapturedAt.Should().Be(Now);

        // Revisão humana obrigatória: nada nasce aprovado (ver ADR-012).
        publication.Status.Should().Be(PublicationStatus.PendingReview);
        publication.IsRead.Should().BeFalse();
        publication.ProcessId.Should().BeNull();
        publication.Type.Should().BeNull();
    }

    [Fact]
    public void Create_RaisesPublicationCapturedEvent_WithoutTheRawContent()
    {
        var publication = TestPublications.Create();

        var capturedEvent = publication.DomainEvents.OfType<PublicationCapturedEvent>().Should().ContainSingle().Subject;
        capturedEvent.PublicationId.Should().Be(publication.Id.Value);
        capturedEvent.TenantId.Should().Be(publication.TenantId);
        capturedEvent.CnjNumber.Should().Be(publication.CnjNumber.Value);
        capturedEvent.ExternalId.Should().Be(publication.ExternalId);

        // O evento é serializado na outbox e propagado por mensageria: o inteiro teor não
        // pode circular por esses caminhos (ver claude.md, seção 14).
        capturedEvent.GetType().GetProperties().Select(property => property.Name)
            .Should().NotContain(nameof(Publication.RawContent));
    }

    [Fact]
    public void Create_TrimsTheExternalId()
    {
        var publication = TestPublications.Create(externalId: "  cnj-7  ");

        publication.ExternalId.Should().Be("cnj-7");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutExternalId_Fails(string? externalId)
    {
        var result = Publication.Create(
            Guid.NewGuid(),
            TestPublications.CnjNumber(),
            externalId!,
            PublicationSource.DJEN,
            TestPublications.RawContent(),
            TestPublications.PublishedAt(),
            capturedAt: Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_REQUIRED_FIELD));
    }

    [Fact]
    public void Create_WithoutTenant_Fails()
    {
        var result = Publication.Create(
            Guid.Empty,
            TestPublications.CnjNumber(),
            "cnj-1",
            PublicationSource.DJEN,
            TestPublications.RawContent(),
            TestPublications.PublishedAt(),
            capturedAt: Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_REQUIRED_FIELD));
    }

    [Fact]
    public void Create_WithPublishedAtInTheFuture_IsBlockedByTheValueObject()
    {
        // A invariante mora em PublishedAt: a data já não consegue ser construída, então uma
        // Publication com data futura é inalcançável por qualquer caminho de código.
        var publishedAtResult = PublishedAt.Create(Now.AddDays(1), Now);

        publishedAtResult.IsFailure.Should().BeTrue();
        publishedAtResult.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_DATE));
    }

    [Fact]
    public void Create_WithEmptyRawContent_IsBlockedByTheValueObject()
    {
        var rawContentResult = RawContent.Create("   ");

        rawContentResult.IsFailure.Should().BeTrue();
        rawContentResult.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_EMPTY_CONTENT));
    }

    [Fact]
    public void MarkAsRead_OnUnreadPublication_Succeeds()
    {
        var publication = TestPublications.Create();

        var result = publication.MarkAsRead();

        result.IsSuccess.Should().BeTrue();
        publication.IsRead.Should().BeTrue();
    }

    [Fact]
    public void MarkAsRead_DoesNotChangeTheReviewStatus()
    {
        // Ler não é revisar: confundir os dois transformaria abrir a publicação em aprovação
        // tácita, o oposto da revisão humana explícita exigida (ver ADR-012).
        var publication = TestPublications.Create();

        publication.MarkAsRead();

        publication.Status.Should().Be(PublicationStatus.PendingReview);
    }

    [Fact]
    public void MarkAsRead_Twice_Fails()
    {
        var publication = TestPublications.Create();
        publication.MarkAsRead();

        var result = publication.MarkAsRead();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_ALREADY_READ));
    }

    [Theory]
    [InlineData(PublicationStatus.Approved)]
    [InlineData(PublicationStatus.Corrected)]
    [InlineData(PublicationStatus.Rejected)]
    [InlineData(PublicationStatus.Archived)]
    public void ChangeStatus_FromPendingReview_IsAllowedForEveryReviewOutcome(PublicationStatus newStatus)
    {
        var publication = TestPublications.Create();

        var result = publication.ChangeStatus(newStatus, "revisado pelo advogado responsável");

        result.IsSuccess.Should().BeTrue();
        publication.Status.Should().Be(newStatus);
        publication.ReviewNotes.Should().Be("revisado pelo advogado responsável");
    }

    [Fact]
    public void ChangeStatus_RaisesPublicationStatusChangedEvent()
    {
        var publication = TestPublications.Create();
        publication.ClearDomainEvents();

        publication.ChangeStatus(PublicationStatus.Approved, "ok");

        var statusEvent = publication.DomainEvents.OfType<PublicationStatusChangedEvent>().Should().ContainSingle().Subject;
        statusEvent.PreviousStatus.Should().Be(PublicationStatus.PendingReview);
        statusEvent.NewStatus.Should().Be(PublicationStatus.Approved);
        statusEvent.Notes.Should().Be("ok");
    }

    [Fact]
    public void ChangeStatus_ToTheSameStatus_Fails()
    {
        var publication = TestPublications.Create();

        var result = publication.ChangeStatus(PublicationStatus.PendingReview);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_STATUS_TRANSITION));
    }

    [Theory]
    [InlineData(PublicationStatus.Approved, PublicationStatus.PendingReview)]
    [InlineData(PublicationStatus.Approved, PublicationStatus.Rejected)]
    [InlineData(PublicationStatus.Rejected, PublicationStatus.Approved)]
    [InlineData(PublicationStatus.Corrected, PublicationStatus.PendingReview)]
    public void ChangeStatus_AfterReview_OnlyAllowsArchiving(PublicationStatus from, PublicationStatus to)
    {
        var publication = TestPublications.Create();
        publication.ChangeStatus(from).IsSuccess.Should().BeTrue();

        var result = publication.ChangeStatus(to);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_INVALID_STATUS_TRANSITION));
    }

    [Fact]
    public void ChangeStatus_FromArchived_IsAlwaysRejected()
    {
        var publication = TestPublications.Create();
        publication.ChangeStatus(PublicationStatus.Archived);

        foreach (var status in Enum.GetValues<PublicationStatus>())
        {
            publication.ChangeStatus(status).IsFailure.Should().BeTrue($"arquivado é terminal, mas aceitou {status}");
        }
    }

    [Fact]
    public void ChangeStatus_WithBlankNotes_StoresNull()
    {
        var publication = TestPublications.Create();

        publication.ChangeStatus(PublicationStatus.Approved, "   ");

        publication.ReviewNotes.Should().BeNull();
    }

    [Fact]
    public void AttachToProcess_OnUnattachedPublication_Succeeds()
    {
        var publication = TestPublications.Create();
        var processId = Guid.NewGuid();

        var result = publication.AttachToProcess(processId);

        result.IsSuccess.Should().BeTrue();
        publication.ProcessId.Should().Be(processId);
    }

    [Fact]
    public void AttachToProcess_ToTheSameProcess_IsIdempotent()
    {
        var publication = TestPublications.Create();
        var processId = Guid.NewGuid();
        publication.AttachToProcess(processId);

        var result = publication.AttachToProcess(processId);

        result.IsSuccess.Should().BeTrue();
        publication.ProcessId.Should().Be(processId);
    }

    [Fact]
    public void AttachToProcess_ToADifferentProcess_Fails()
    {
        var publication = TestPublications.Create();
        publication.AttachToProcess(Guid.NewGuid());

        var result = publication.AttachToProcess(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.COMMON_INVALID_OPERATION));
    }

    [Fact]
    public void AttachToProcess_WithEmptyProcessId_Fails()
    {
        var publication = TestPublications.Create();

        var result = publication.AttachToProcess(Guid.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_REQUIRED_FIELD));
    }

    [Fact]
    public void LinkTo_RecordsProcessClienteAndResponsavel()
    {
        var publication = TestPublications.Create();
        var (processId, clienteId, responsavelId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var result = publication.LinkTo(processId, clienteId, responsavelId);

        result.IsSuccess.Should().BeTrue();
        publication.ProcessId.Should().Be(processId);
        publication.ClienteId.Should().Be(clienteId);
        publication.ResponsavelUserId.Should().Be(responsavelId);
    }

    [Fact]
    public void LinkTo_TreatsEmptyGuidsAsAbsent()
    {
        var publication = TestPublications.Create();

        publication.LinkTo(Guid.NewGuid(), Guid.Empty, Guid.Empty);

        publication.ClienteId.Should().BeNull();
        publication.ResponsavelUserId.Should().BeNull();
    }

    [Fact]
    public void LinkTo_ToADifferentProcess_FailsAndKeepsTheOriginalLinks()
    {
        var publication = TestPublications.Create();
        var firstProcess = Guid.NewGuid();
        var cliente = Guid.NewGuid();
        publication.LinkTo(firstProcess, cliente, null);

        var result = publication.LinkTo(Guid.NewGuid(), Guid.NewGuid(), null);

        result.IsFailure.Should().BeTrue();
        publication.ProcessId.Should().Be(firstProcess);
        publication.ClienteId.Should().Be(cliente);
    }

    [Fact]
    public void Create_DerivesTribunalDigitsAndPublishedOnFromTheSources()
    {
        var publication = TestPublications.Create();

        publication.Tribunal.Should().Be("TJSP");
        publication.CnjDigits.Should().Be(publication.CnjNumber.Digits);
        publication.PublishedOn.Should().Be(publication.PublishedAt.ToDateOnly());
    }

    [Fact]
    public void SetType_RecordsTheActType()
    {
        var publication = TestPublications.Create();

        publication.SetType(PublicationType.Intimacao);

        publication.Type.Should().Be(PublicationType.Intimacao);
    }
}
