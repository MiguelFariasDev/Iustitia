using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Errors.Exceptions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Ai;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Domain.Suggestions;
using Advocacia.Modules.Legal.Infrastructure.Jobs;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Advocacia.Modules.Legal.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Jobs;

public class PublicationClassificationJobTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Publication _publication;
    private readonly IPublicationSuggestionRepository _suggestions = Substitute.For<IPublicationSuggestionRepository>();
    private readonly IAnthropicUsageQuery _usageQuery = Substitute.For<IAnthropicUsageQuery>();
    private readonly IProcessoRepository _processos = Substitute.For<IProcessoRepository>();
    private readonly IClassificationService _classification = Substitute.For<IClassificationService>();
    private readonly IDeadlineSuggestionService _deadline = Substitute.For<IDeadlineSuggestionService>();
    private readonly ILegalUnitOfWork _unitOfWork = Substitute.For<ILegalUnitOfWork>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly LegalDbContext _dbContext;
    private readonly AiUsageContext _usageContext = new();
    private readonly List<PublicationSuggestion> _added = [];

    public PublicationClassificationJobTests()
    {
        _publication = Publication.Create(
            _tenantId,
            CNJNumber.Create("1234567-48.2026.8.26.0001").Value,
            "ext-1",
            PublicationSource.DJEN,
            RawContent.Create("Fica a parte intimada a manifestar-se no prazo de 15 dias.").Value,
            PublishedAt.Create(Now.AddDays(-1), Now).Value,
            capturedAt: Now).Value;

        var dbOptions = new DbContextOptionsBuilder<LegalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new LegalDbContext(dbOptions, _tenantContext);

        _suggestions.ListTenantsAwaitingSuggestionAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { _tenantId });
        _suggestions.ListAwaitingSuggestionAsync(_tenantId, 0, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Publication> { _publication });
        _usageQuery.CountFailuresByReferenceAsync(
                _tenantId, "classification", "AI_INVALID_RESPONSE", Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int>());
        _suggestions.When(repository => repository.Add(Arg.Any<PublicationSuggestion>()))
            .Do(call => _added.Add(call.Arg<PublicationSuggestion>()));
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task ExecuteAsync_SavesASuggestionWithTheLowestConfidenceAndTheRefinedDeadline()
    {
        Classified(confidence: 80, days: 15, DeadlineType.Business);
        Deadline(days: 8, DeadlineType.Calendar, confidence: 60);

        await CreateJob().ExecuteAsync(CancellationToken.None);

        var suggestion = _added.Should().ContainSingle().Subject;
        suggestion.TenantId.Should().Be(_tenantId);
        suggestion.PublicationId.Should().Be(_publication.Id);
        suggestion.ActType.Should().Be("intimacao");
        suggestion.SuggestedDeadlineDays.Should().Be(8, "o prazo refinado vale mais que o da classificação");
        suggestion.DeadlineType.Should().Be(DeadlineType.Calendar);
        suggestion.Confidence.Should().Be(60, "é o menor entre a confiança da classificação e a do prazo");
        suggestion.RequiresManualReview.Should().BeFalse();
        suggestion.PromptVersion.Should().Be("1.0.0/1.1.0");
        suggestion.AiProvider.Should().Be("Anthropic");
        suggestion.RawResponse.Should().Contain("\"classification\"").And.Contain("\"deadline\"");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _tenantContext.Received().SetTenant(_tenantId);
    }

    [Fact]
    public async Task ExecuteAsync_NeverReviewsTheLegalActItself()
    {
        // Revisão humana obrigatória (ADR-012): a IA só sugere. Nada de aprovar, tipar ou
        // mudar o estado da publicação.
        Classified(confidence: 99, days: 15, DeadlineType.Business);
        Deadline(days: 15, DeadlineType.Business, confidence: 99);

        await CreateJob().ExecuteAsync(CancellationToken.None);

        _publication.Status.Should().Be(PublicationStatus.PendingReview);
        _publication.Type.Should().BeNull();
        _publication.ReviewNotes.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_LowConfidence_IsSavedAsPendingManualReview()
    {
        Classified(confidence: 40, days: 15, DeadlineType.Business);
        Deadline(days: 15, DeadlineType.Business, confidence: 90);

        await CreateJob().ExecuteAsync(CancellationToken.None);

        var suggestion = _added.Should().ContainSingle().Subject;
        suggestion.Confidence.Should().Be(40);
        suggestion.RequiresManualReview.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_LinksAiUsageToThePublicationOnlyWhileProcessingIt()
    {
        Guid? seenDuringClassification = null;
        _classification.ClassifyAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                seenDuringClassification = _usageContext.PublicationId;
                return Result.Success(ClassificationOf(80, 15, DeadlineType.Business));
            });
        Deadline(days: 15, DeadlineType.Business, confidence: 80);

        await CreateJob().ExecuteAsync(CancellationToken.None);

        seenDuringClassification.Should().Be(_publication.Id.Value);
        _usageContext.PublicationId.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DeadlineStepInvalid_StillSavesWithTheClassificationDeadline()
    {
        // A classificação já foi paga e é válida: não se descarta nem se paga de novo.
        Classified(confidence: 85, days: 15, DeadlineType.Business);
        _deadline.SuggestAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DeadlineSuggestion>(ErrorFactory.From(ErrorCode.AI_INVALID_RESPONSE)));

        var act = () => CreateJob().ExecuteAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        var suggestion = _added.Should().ContainSingle().Subject;
        suggestion.SuggestedDeadlineDays.Should().Be(15);
        suggestion.Confidence.Should().Be(85);
        suggestion.PromptVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidClassification_SkipsThePublicationWithoutFailingTheJob()
    {
        _classification.ClassifyAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ClassificationResult>(ErrorFactory.From(ErrorCode.AI_INVALID_RESPONSE)));

        var act = () => CreateJob().ExecuteAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        _added.Should().BeEmpty();
        await _deadline.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(ErrorCode.AI_UNAVAILABLE)]
    [InlineData(ErrorCode.AI_TIMEOUT)]
    [InlineData(ErrorCode.AI_RATE_LIMITED)]
    [InlineData(ErrorCode.AI_UNAUTHORIZED)]
    [InlineData(ErrorCode.AI_OVERLOADED)]
    public async Task ExecuteAsync_ProviderOutage_StopsAndRethrowsSoHangfireRetries(ErrorCode code)
    {
        _classification.ClassifyAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ClassificationResult>(ErrorFactory.From(code)));

        var act = () => CreateJob().ExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<IntegrationException>();
        _added.Should().BeEmpty();
        await _deadline.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_PublicationThatExhaustedItsAttempts_IsNotSentToTheModelAgain()
    {
        _usageQuery.CountFailuresByReferenceAsync(
                _tenantId, "classification", "AI_INVALID_RESPONSE", Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [_publication.Id.Value] = 3 });

        await CreateJob().ExecuteAsync(CancellationToken.None);

        await _classification.DidNotReceiveWithAnyArgs().ClassifyAsync(default!, default, default, default);
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_DoesNothing()
    {
        await CreateJob(new PublicationClassificationJobOptions { Enabled = false }).ExecuteAsync(CancellationToken.None);

        await _suggestions.DidNotReceiveWithAnyArgs().ListTenantsAwaitingSuggestionAsync(default);
        await _classification.DidNotReceiveWithAnyArgs().ClassifyAsync(default!, default, default, default);
    }

    [Fact]
    public async Task ExecuteAsync_NothingPending_DoesNothing()
    {
        _suggestions.ListTenantsAwaitingSuggestionAsync(Arg.Any<CancellationToken>()).Returns(new List<Guid>());

        await CreateJob().ExecuteAsync(CancellationToken.None);

        await _classification.DidNotReceiveWithAnyArgs().ClassifyAsync(default!, default, default, default);
    }

    private PublicationClassificationJob CreateJob(PublicationClassificationJobOptions? options = null)
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(Now);

        return new PublicationClassificationJob(
            _suggestions,
            _usageQuery,
            _processos,
            _classification,
            _deadline,
            _usageContext,
            _unitOfWork,
            _dbContext,
            _tenantContext,
            dateTimeProvider,
            Options.Create(options ?? new PublicationClassificationJobOptions()),
            NullLogger<PublicationClassificationJob>.Instance);
    }

    private void Classified(int confidence, int? days, DeadlineType? type) =>
        _classification.ClassifyAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(ClassificationOf(confidence, days, type)));

    private void Deadline(int? days, DeadlineType? type, int confidence) =>
        _deadline.SuggestAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new DeadlineSuggestion(
                days,
                type,
                "CPC, art. 219",
                confidence,
                confidence < 50,
                false,
                new AiCallMetadata("Anthropic", "modelo-x", "1.1.0"),
                "{\"deadline_days\":8}")));

    private static ClassificationResult ClassificationOf(int confidence, int? days, DeadlineType? type) =>
        new("intimacao",
            days,
            type,
            "CPC, art. 1.003",
            confidence,
            confidence < 50,
            false,
            new AiCallMetadata("Anthropic", "modelo-x", "1.0.0"),
            "{\"act_type\":\"intimacao\"}");
}
