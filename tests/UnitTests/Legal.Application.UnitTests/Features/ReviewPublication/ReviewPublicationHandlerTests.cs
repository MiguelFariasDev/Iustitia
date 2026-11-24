using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.ReviewPublication;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Suggestions;
using FluentAssertions;
using NSubstitute;

namespace Advocacia.Modules.Legal.Application.UnitTests.Features.ReviewPublication;

public sealed class ReviewPublicationHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();

    private readonly IPublicationRepository _publications = Substitute.For<IPublicationRepository>();
    private readonly IPublicationSuggestionRepository _suggestions = Substitute.For<IPublicationSuggestionRepository>();
    private readonly IProcessoRepository _processos = Substitute.For<IProcessoRepository>();
    private readonly IPrazoAgendador _agendador = Substitute.For<IPrazoAgendador>();
    private readonly ILegalUnitOfWork _unitOfWork = Substitute.For<ILegalUnitOfWork>();
    private readonly List<string> _ordem = [];

    public ReviewPublicationHandlerTests()
    {
        _agendador.AgendarAsync(Arg.Any<AgendarPrazoRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _ordem.Add("agendar");

                return Result.Success(EventId);
            });
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            _ordem.Add("salvar");

            return 1;
        });
    }

    private ReviewPublicationHandler Handler() => new(_publications, _suggestions, _processos, _agendador, _unitOfWork);

    private Publication NovaPublicacao(DateTimeOffset? publicadaEm = null)
    {
        var agora = TestPublications.Now;
        var publication = Publication.Create(
            Tenant,
            Advocacia.Modules.Legal.Domain.Shared.CNJNumber.Create(TestPublications.ValidCnjNumber).Value,
            "ext-1",
            PublicationSource.DJEN,
            RawContent.Create("Fica o réu intimado para contestar em 15 dias.").Value,
            PublishedAt.Create(publicadaEm ?? new DateTimeOffset(2026, 11, 20, 15, 0, 0, TimeSpan.Zero), agora.AddYears(1)).Value,
            capturedAt: agora).Value;
        _publications.GetByIdAsync(publication.Id, Arg.Any<CancellationToken>()).Returns(publication);

        return publication;
    }

    private void ComSugestao(Publication publication, int? dias = 15, DeadlineType tipo = DeadlineType.Business, int confianca = 90)
    {
        var sugestao = PublicationSuggestion.Create(
            Tenant, publication.Id, "Contestação", dias, dias is null ? null : tipo, "CPC, art. 335", confianca,
            false, "anthropic", "modelo", "v1", "{}", TestPublications.Now).Value;
        _suggestions.GetByPublicationIdAsync(Tenant, publication.Id, Arg.Any<CancellationToken>()).Returns(sugestao);
    }

    [Fact]
    public async Task Approve_UsesTheSuggestedDeadline_ComputesTheDueDateInBusinessDays_AndSchedulesBeforeSaving()
    {
        var publication = NovaPublicacao(); // sexta 20/11/2026 15:00 UTC (12:00 em Brasília)
        ComSugestao(publication, 15);

        var result = await Handler().Handle(new ReviewPublicationCommand(publication.Id.Value, "Approved"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Approved");
        result.Value.Days.Should().Be(15);
        result.Value.DeadlineKind.Should().Be("business");
        result.Value.EventId.Should().Be(EventId);
        // Disponibilizado sex 20/11 (feriado da Consciência Negra) → publicação seg 23/11 (Lei 11.419, art. 4º, §3º);
        // a contagem começa ter 24/11 (CPC, art. 224, §3º): 4 dias em novembro (24–27), 30/11 é o 5º,
        // 1–4/12 do 6º ao 9º, 7–11/12 do 10º ao 14º e o 15º dia útil é seg 14/12.
        result.Value.Vencimento.Should().Be(new DateOnly(2026, 12, 14));
        publication.Status.Should().Be(PublicationStatus.Approved);
        _ordem.Should().Equal("agendar", "salvar");
    }

    [Fact]
    public async Task Approve_SchedulesTheDeadlineForTheProcessOwner_WithoutTheTextOfThePublication()
    {
        var publication = NovaPublicacao();
        ComSugestao(publication, 5);
        AgendarPrazoRequest? enviado = null;
        _agendador.AgendarAsync(Arg.Do<AgendarPrazoRequest>(r => enviado = r), Arg.Any<CancellationToken>()).Returns(Result.Success(EventId));

        await Handler().Handle(new ReviewPublicationCommand(publication.Id.Value, "Approved"), default);

        enviado!.TenantId.Should().Be(Tenant);
        enviado.PublicationId.Should().Be(publication.Id.Value);
        enviado.Titulo.Should().Be($"Prazo: Contestação — {TestPublications.ValidCnjNumber}");
        enviado.Descricao.Should().Contain("ESTIMATIVA").And.NotContain("réu");
    }

    [Fact]
    public async Task Correct_UsesTheLawyersDeadline_AndRecordsWhatTheAiSuggested()
    {
        var publication = NovaPublicacao();
        ComSugestao(publication, 15);

        var result = await Handler().Handle(new ReviewPublicationCommand(publication.Id.Value, "Corrected", 10, "Juiz fixou 10 dias"), default);

        result.Value.Days.Should().Be(10);
        publication.Status.Should().Be(PublicationStatus.Corrected);
        publication.ReviewNotes.Should().Contain("IA sugeriu: Contestação, 15 dias").And.Contain("10 dias úteis").And.Contain("Juiz fixou 10 dias");
    }

    [Fact]
    public async Task Correct_WithoutADeadline_ChangesTheStatusButSchedulesNothing()
    {
        var publication = NovaPublicacao();
        ComSugestao(publication, 15);

        var result = await Handler().Handle(new ReviewPublicationCommand(publication.Id.Value, "Corrected"), default);

        result.Value.EventId.Should().BeNull();
        result.Value.Vencimento.Should().BeNull();
        await _agendador.DidNotReceiveWithAnyArgs().AgendarAsync(default!, default);
        _ordem.Should().Equal("salvar");
    }

    [Fact]
    public async Task Approve_WhenTheAiFoundNoDeadline_ApprovesWithoutScheduling()
    {
        var publication = NovaPublicacao();
        ComSugestao(publication, dias: null);

        var result = await Handler().Handle(new ReviewPublicationCommand(publication.Id.Value, "Approved"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.EventId.Should().BeNull();
        await _agendador.DidNotReceiveWithAnyArgs().AgendarAsync(default!, default);
    }

    [Fact]
    public async Task Reject_NeverSchedules_EvenIfADeadlineIsSent()
    {
        var publication = NovaPublicacao();
        ComSugestao(publication, 15);

        var result = await Handler().Handle(new ReviewPublicationCommand(publication.Id.Value, "Rejected", 15), default);

        result.Value.Status.Should().Be("Rejected");
        await _agendador.DidNotReceiveWithAnyArgs().AgendarAsync(default!, default);
    }

    [Fact]
    public async Task CalendarDays_AreCountedAsCalendarDays()
    {
        var publication = NovaPublicacao();
        ComSugestao(publication, 10, DeadlineType.Calendar);

        var result = await Handler().Handle(new ReviewPublicationCommand(publication.Id.Value, "Approved"), default);

        result.Value.DeadlineKind.Should().Be("calendar");
        // Começa em 24/11 e o 10º dia corrido é quinta 03/12.
        result.Value.Vencimento.Should().Be(new DateOnly(2026, 12, 3));
    }

    [Fact]
    public async Task WhenSchedulingFails_TheDecisionIsNotSaved()
    {
        var publication = NovaPublicacao();
        ComSugestao(publication, 15);
        _agendador.AgendarAsync(Arg.Any<AgendarPrazoRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid>(ErrorFactory.From(ErrorCode.COMMON_UNEXPECTED)));

        var result = await Handler().Handle(new ReviewPublicationCommand(publication.Id.Value, "Approved"), default);

        result.IsFailure.Should().BeTrue();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AnAlreadyReviewedPublication_IsRefused_BeforeSchedulingAnything()
    {
        var publication = NovaPublicacao();
        ComSugestao(publication, 15);
        publication.ChangeStatus(PublicationStatus.Approved);

        var result = await Handler().Handle(new ReviewPublicationCommand(publication.Id.Value, "Approved"), default);

        result.Error.Code.Should().Be(ErrorCode.PUBLICATION_INVALID_STATUS_TRANSITION.ToString());
        await _agendador.DidNotReceiveWithAnyArgs().AgendarAsync(default!, default);
    }

    [Fact]
    public async Task UnknownPublication_IsNotFound_AndUnknownDecisionIsInvalid()
    {
        (await Handler().Handle(new ReviewPublicationCommand(Guid.NewGuid(), "Approved"), default))
            .Error.Code.Should().Be(ErrorCode.PUBLICATION_NOT_FOUND.ToString());

        var publication = NovaPublicacao();
        (await Handler().Handle(new ReviewPublicationCommand(publication.Id.Value, "Archived"), default))
            .Error.Code.Should().Be(ErrorCode.VALIDATION_INVALID_ENUM.ToString());
    }
}
