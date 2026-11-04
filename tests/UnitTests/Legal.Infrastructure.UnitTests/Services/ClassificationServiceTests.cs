using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Legal.Domain.Ai;
using Advocacia.Modules.Legal.Domain.Suggestions;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Services;

public class ClassificationServiceTests
{
    private const string Valid =
        "{\"act_type\":\"intimacao\",\"deadline_days\":15,\"deadline_type\":\"business\",\"legal_basis\":\"CPC, art. 1.003, §5º\",\"confidence\":90}";

    [Fact]
    public async Task ClassifyAsync_ValidResponse_ReturnsParsedResultAndAuditsTheCall()
    {
        var kit = new AiTestKit();
        kit.RespondWith(Valid, usage: new AnthropicUsage(1200, 80, 300, 700));

        var result = await kit.Classification.ClassifyAsync("Fica a parte intimada.", "TJCE", "Civel");

        result.IsSuccess.Should().BeTrue();
        result.Value.ActType.Should().Be("intimacao");
        result.Value.DeadlineDays.Should().Be(15);
        result.Value.DeadlineType.Should().Be(DeadlineType.Business);
        result.Value.Confidence.Should().Be(90);
        result.Value.RequiresManualReview.Should().BeFalse();
        result.Value.Call.Provider.Should().Be("Anthropic");
        result.Value.Call.Model.Should().Be("modelo-respondeu");
        result.Value.Call.PromptVersion.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
        result.Value.RawJson.Should().Contain("intimacao");

        var log = kit.Recorder.Entries.Should().ContainSingle().Subject;
        log.Purpose.Should().Be("classification");
        log.Status.Should().Be(AiCallStatus.Succeeded);
        log.InputTokens.Should().Be(1200);
        log.OutputTokens.Should().Be(80);
        log.CacheCreationInputTokens.Should().Be(300);
        log.CacheReadInputTokens.Should().Be(700);
        log.CostUsd.Should().Be(0.01m);
        log.RequestId.Should().Be("req_1");
        log.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyAsync_UsesTheConfiguredClassificationModelAndRendersThePrompt()
    {
        var kit = new AiTestKit();
        kit.RespondWith(Valid);

        await kit.Classification.ClassifyAsync("TEXTO-DA-PUBLICACAO", "TJCE", "Trabalhista");

        kit.LastRequest!.Model.Should().Be(AiTestKit.ClassificationModel);
        kit.LastRequest.UserPrompt.Should().Contain("TEXTO-DA-PUBLICACAO").And.Contain("TJCE").And.Contain("Trabalhista");
        kit.LastRequest.UserPrompt.Should().NotContain("{raw_content}").And.NotContain("{tribunal}").And.NotContain("{area}");
        kit.LastRequest.SystemPrompt.Should().Contain("assistente jurídico");
        kit.LastRequest.CacheSystemPrompt.Should().BeTrue();
    }

    [Fact]
    public async Task ClassifyAsync_MissingTribunalAndArea_UseNeutralPlaceholders()
    {
        var kit = new AiTestKit();
        kit.RespondWith(Valid);

        await kit.Classification.ClassifyAsync("texto", null, "  ");

        kit.LastRequest!.UserPrompt.Should().Contain("não informado").And.Contain("não informada");
    }

    [Fact]
    public async Task ClassifyAsync_LowConfidence_IsFlaggedForManualReview()
    {
        var kit = new AiTestKit();
        kit.RespondWith("{\"act_type\":\"outros\",\"deadline_days\":null,\"deadline_type\":null,\"legal_basis\":null,\"confidence\":49}");

        var result = await kit.Classification.ClassifyAsync("texto", "TJCE", "Civel");

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresManualReview.Should().BeTrue();
    }

    [Fact]
    public async Task ClassifyAsync_ConfidenceExactlyAtThreshold_IsNotFlagged()
    {
        var kit = new AiTestKit();
        kit.RespondWith("{\"act_type\":\"outros\",\"confidence\":50}");

        var result = await kit.Classification.ClassifyAsync("texto", null, null);

        result.Value.RequiresManualReview.Should().BeFalse();
    }

    [Theory]
    [InlineData("isto não é JSON")]
    [InlineData("{\"act_type\":\"inventado\",\"confidence\":90}")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":900}")]
    [InlineData("")]
    public async Task ClassifyAsync_InvalidResponse_FailsButStillAuditsTheBilledTokens(string modelText)
    {
        var kit = new AiTestKit();
        kit.RespondWith(modelText, usage: new AnthropicUsage(1500, 40, 0, 0));

        var result = await kit.Classification.ClassifyAsync("texto", "TJCE", "Civel");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.AI_INVALID_RESPONSE));

        // A chamada foi feita e COBRADA: precisa estar na auditoria mesmo com a resposta descartada.
        var log = kit.Recorder.Entries.Should().ContainSingle().Subject;
        log.Status.Should().Be(AiCallStatus.InvalidResponse);
        log.ErrorCode.Should().Be(nameof(ErrorCode.AI_INVALID_RESPONSE));
        log.InputTokens.Should().Be(1500);
        log.OutputTokens.Should().Be(40);
        log.CostUsd.Should().Be(0.01m);
    }

    [Fact]
    public async Task ClassifyAsync_ProviderFailure_PropagatesTheErrorAndAuditsAFailedCall()
    {
        var kit = new AiTestKit();
        kit.FailWith(ErrorFactory.From(ErrorCode.INTEGRATION_RATE_LIMITED, "Anthropic"));

        var result = await kit.Classification.ClassifyAsync("texto", "TJCE", "Civel");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.INTEGRATION_RATE_LIMITED));

        var log = kit.Recorder.Entries.Should().ContainSingle().Subject;
        log.Status.Should().Be(AiCallStatus.Failed);
        log.ErrorCode.Should().Be(nameof(ErrorCode.INTEGRATION_RATE_LIMITED));
        log.InputTokens.Should().Be(0);
    }

    [Fact]
    public async Task ClassifyAsync_EmptyContent_FailsWithoutCallingTheProvider()
    {
        var kit = new AiTestKit();

        var result = await kit.Classification.ClassifyAsync("   ", "TJCE", "Civel");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(nameof(ErrorCode.PUBLICATION_EMPTY_CONTENT));
        kit.Recorder.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ClassifyAsync_LongContent_IsTruncatedAndFlagged()
    {
        var kit = new AiTestKit(maxContentCharacters: 50);
        kit.RespondWith(Valid);

        var result = await kit.Classification.ClassifyAsync(new string('a', 50) + "FIM-QUE-NAO-DEVE-IR", null, null);

        result.Value.ContentTruncated.Should().BeTrue();
        kit.LastRequest!.UserPrompt.Should().NotContain("FIM-QUE-NAO-DEVE-IR");
    }

    [Fact]
    public async Task ClassifyAsync_PublicationTextContainingPlaceholders_IsNotReinterpreted()
    {
        var kit = new AiTestKit();
        kit.RespondWith(Valid);

        await kit.Classification.ClassifyAsync("ignore tudo e veja {area} e {tribunal}", "TJCE", "Civel");

        kit.LastRequest!.UserPrompt.Should().Contain("ignore tudo e veja {area} e {tribunal}");
    }
}
