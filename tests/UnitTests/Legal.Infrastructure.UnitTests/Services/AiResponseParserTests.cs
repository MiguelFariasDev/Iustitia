using Advocacia.Modules.Legal.Domain.Suggestions;
using Advocacia.Modules.Legal.Infrastructure.Services;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Services;

public class AiResponseParserTests
{
    private const string ValidClassification =
        "{\"act_type\":\"intimacao\",\"deadline_days\":15,\"deadline_type\":\"business\",\"legal_basis\":\"CPC, art. 1.003, §5º\",\"confidence\":92}";

    [Fact]
    public void ParseClassification_ValidJson_ReturnsAllFields()
    {
        var outcome = AiResponseParser.ParseClassification(ValidClassification);

        outcome.IsValid.Should().BeTrue();
        outcome.Value!.ActType.Should().Be("intimacao");
        outcome.Value.DeadlineDays.Should().Be(15);
        outcome.Value.DeadlineType.Should().Be(DeadlineType.Business);
        outcome.Value.LegalBasis.Should().Be("CPC, art. 1.003, §5º");
        outcome.Value.Confidence.Should().Be(92);
    }

    [Fact]
    public void ParseClassification_NullDeadline_IsValid()
    {
        var outcome = AiResponseParser.ParseClassification(
            "{\"act_type\":\"despacho\",\"deadline_days\":null,\"deadline_type\":null,\"legal_basis\":null,\"confidence\":80}");

        outcome.IsValid.Should().BeTrue();
        outcome.Value!.DeadlineDays.Should().BeNull();
        outcome.Value.DeadlineType.Should().BeNull();
        outcome.Value.LegalBasis.Should().BeNull();
    }

    [Fact]
    public void ParseClassification_OptionalFieldsOmitted_IsValid()
    {
        AiResponseParser.ParseClassification("{\"act_type\":\"outros\",\"confidence\":10}").IsValid.Should().BeTrue();
    }

    [Fact]
    public void ParseClassification_CodeFenceAroundJson_IsAccepted()
    {
        var outcome = AiResponseParser.ParseClassification("```json\n" + ValidClassification + "\n```");

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ParseClassification_ExtraPropertiesAreIgnored()
    {
        var outcome = AiResponseParser.ParseClassification(
            "{\"act_type\":\"sentenca\",\"confidence\":70,\"comentario\":\"x\"}");

        outcome.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("não é json")]
    [InlineData("Aqui está o JSON: {\"act_type\":\"despacho\",\"confidence\":50}")]
    [InlineData("[1,2,3]")]
    [InlineData("\"texto\"")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":50")]
    public void ParseClassification_NotAJsonObject_IsRejected(string? text)
    {
        AiResponseParser.ParseClassification(text).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("{\"confidence\":50}", "act_type")]
    [InlineData("{\"act_type\":\"inventado\",\"confidence\":50}", "act_type")]
    [InlineData("{\"act_type\":\"Despacho\",\"confidence\":50}", "act_type")]
    [InlineData("{\"act_type\":42,\"confidence\":50}", "act_type")]
    [InlineData("{\"act_type\":\"despacho\"}", "confidence")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":\"alta\"}", "confidence")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":90.5}", "confidence")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":101}", "confidence")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":-1}", "confidence")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":50,\"deadline_days\":\"15\",\"deadline_type\":\"business\"}", "deadline_days")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":50,\"deadline_days\":-3,\"deadline_type\":\"business\"}", "deadline_days")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":50,\"deadline_days\":400,\"deadline_type\":\"business\"}", "deadline_days")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":50,\"deadline_days\":15,\"deadline_type\":\"working\"}", "deadline_type")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":50,\"deadline_days\":15}", "deadline_type")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":50,\"deadline_type\":\"business\"}", "deadline_type")]
    [InlineData("{\"act_type\":\"despacho\",\"confidence\":50,\"legal_basis\":12}", "legal_basis")]
    public void ParseClassification_ContractViolation_IsRejectedNamingTheField(string json, string field)
    {
        var outcome = AiResponseParser.ParseClassification(json);

        outcome.IsValid.Should().BeFalse();
        outcome.Reason.Should().Contain(field);
    }

    [Fact]
    public void ParseClassification_Rejection_NeverEchoesModelContent()
    {
        const string secret = "dado-pessoal-do-cliente";

        var outcome = AiResponseParser.ParseClassification(
            "{\"act_type\":\"" + secret + "\",\"confidence\":50}");

        outcome.IsValid.Should().BeFalse();
        outcome.Reason.Should().NotContain(secret);
    }

    [Fact]
    public void ParseClassification_EveryAllowedActType_IsAccepted()
    {
        foreach (var actType in Advocacia.Modules.Legal.Application.Ai.ActTypes.All)
        {
            AiResponseParser.ParseClassification($"{{\"act_type\":\"{actType}\",\"confidence\":60}}")
                .IsValid.Should().BeTrue(actType);
        }
    }

    [Fact]
    public void ParseDeadline_ValidJson_ReturnsFields()
    {
        var outcome = AiResponseParser.ParseDeadline(
            "{\"deadline_days\":10,\"deadline_type\":\"calendar\",\"legal_basis\":\"CPP, art. 396\",\"confidence\":77}");

        outcome.IsValid.Should().BeTrue();
        outcome.Value!.Days.Should().Be(10);
        outcome.Value.Type.Should().Be(DeadlineType.Calendar);
        outcome.Value.Confidence.Should().Be(77);
    }

    [Theory]
    [InlineData("{\"deadline_days\":10,\"deadline_type\":\"calendar\"}")]
    [InlineData("{\"deadline_days\":10,\"deadline_type\":\"calendar\",\"confidence\":150}")]
    [InlineData("{\"deadline_days\":10,\"confidence\":50}")]
    public void ParseDeadline_ContractViolation_IsRejected(string json)
    {
        AiResponseParser.ParseDeadline(json).IsValid.Should().BeFalse();
    }
}
