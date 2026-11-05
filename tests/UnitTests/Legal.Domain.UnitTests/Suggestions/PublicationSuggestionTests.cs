using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Suggestions;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Domain.UnitTests.Suggestions;

public class PublicationSuggestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static Advocacia.BuildingBlocks.Domain.Results.Result<PublicationSuggestion> Create(
        Guid? tenantId = null,
        string actType = "intimacao",
        int? days = 15,
        DeadlineType? type = DeadlineType.Business,
        int confidence = 90,
        string? legalBasis = "CPC, art. 1.003, §5º") =>
        PublicationSuggestion.Create(
            tenantId ?? Guid.NewGuid(),
            PublicationId.New(),
            actType,
            days,
            type,
            legalBasis,
            confidence,
            contentTruncated: false,
            "Anthropic",
            "claude-sonnet-5-5",
            "1.0.0/1.0.0",
            "{\"classification\":{}}",
            Now);

    [Fact]
    public void Create_WithValidData_Succeeds()
    {
        var result = Create();

        result.IsSuccess.Should().BeTrue();
        result.Value.ActType.Should().Be("intimacao");
        result.Value.SuggestedDeadlineDays.Should().Be(15);
        result.Value.DeadlineType.Should().Be(DeadlineType.Business);
        result.Value.RequiresManualReview.Should().BeFalse();
        result.Value.CreatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(49, true)]
    [InlineData(50, false)]
    [InlineData(100, false)]
    public void Create_ConfidenceBelowThreshold_RequiresManualReview(int confidence, bool expected)
    {
        var result = Create(confidence: confidence);

        result.Value.RequiresManualReview.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_ConfidenceOutOfRange_Fails(int confidence)
    {
        var result = Create(confidence: confidence);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.VALIDATION_OUT_OF_RANGE));
    }

    [Fact]
    public void Create_DeadlineWithoutType_Fails()
    {
        // Prazo e forma de contagem andam juntos.
        Create(days: 15, type: null).IsFailure.Should().BeTrue();
        Create(days: null, type: DeadlineType.Calendar).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutDeadline_IsAllowed()
    {
        var result = Create(actType: "despacho", days: null, type: null, legalBasis: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.SuggestedDeadlineDays.Should().BeNull();
        result.Value.LegalBasis.Should().BeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Create_DeadlineOutOfRange_Fails(int days)
    {
        Create(days: days).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_EmptyTenantOrActType_Fails()
    {
        Create(tenantId: Guid.Empty).IsFailure.Should().BeTrue();
        Create(actType: "  ").IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("business", true, DeadlineType.Business)]
    [InlineData("calendar", true, DeadlineType.Calendar)]
    [InlineData(" Business ", true, DeadlineType.Business)]
    [InlineData("working", false, default(DeadlineType))]
    [InlineData(null, false, default(DeadlineType))]
    public void DeadlineType_TryParse_AcceptsOnlyTheContractValues(string? value, bool ok, DeadlineType expected)
    {
        DeadlineTypeExtensions.TryParse(value, out var parsed).Should().Be(ok);

        if (ok)
        {
            parsed.Should().Be(expected);
        }
    }

    [Fact]
    public void DeadlineType_ToValue_IsLowercase()
    {
        DeadlineType.Business.ToValue().Should().Be("business");
        DeadlineType.Calendar.ToValue().Should().Be("calendar");
    }
}
