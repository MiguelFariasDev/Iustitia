using Advocacia.Modules.Legal.Application.Ai;
using Advocacia.Modules.Workflow.Infrastructure;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Services;

public class EmbeddedPromptCatalogTests
{
    private readonly EmbeddedPromptCatalog _catalog = new();

    [Fact]
    public void Get_Classification_LoadsVersionedPromptWithExpectedPlaceholders()
    {
        var result = _catalog.Get("classification");

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
        result.Value.SystemPrompt.Should().Contain("assistente jurídico especializado em direito brasileiro");
        result.Value.SystemPrompt.Should().Contain("Diário de Justiça Eletrônico Nacional (DJEN)");
        result.Value.UserTemplate.Should().Contain("{raw_content}").And.Contain("{tribunal}").And.Contain("{area}");
    }

    [Fact]
    public void Get_Deadline_LoadsVersionedPromptWithExpectedPlaceholders()
    {
        var result = _catalog.Get("deadline");

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
        result.Value.SystemPrompt.Should().Contain("assistente jurídico");
        result.Value.UserTemplate.Should().Contain("{raw_content}").And.Contain("{act_type}");
    }

    [Fact]
    public void Get_Classification_ListsExactlyTheActTypesTheValidatorAccepts()
    {
        // Prompt e validador precisam andar juntos: um tipo no prompt que o validador rejeita
        // (ou o contrário) viraria resposta descartada — e paga — em produção.
        var system = _catalog.Get("classification").Value.SystemPrompt;

        foreach (var actType in ActTypes.All)
        {
            system.Should().Contain($"\"{actType}\"", $"o prompt deve listar '{actType}'");
        }
    }

    [Theory]
    [InlineData("classification")]
    [InlineData("deadline")]
    public void Get_PromptsDeclareStrictJsonOutputAndTreatPublicationAsData(string name)
    {
        var system = _catalog.Get(name).Value.SystemPrompt;

        system.Should().Contain("SOMENTE com um objeto JSON válido");
        system.Should().Contain("<publicacao>");
        system.Should().Contain("DADO");
    }

    [Fact]
    public void Get_UnknownPrompt_FailsWithCatalogError()
    {
        var result = _catalog.Get("nao-existe");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AI_PROMPT_NOT_FOUND");
    }

    [Fact]
    public void Get_CalledTwice_ReturnsTheSameParsedTemplate()
    {
        _catalog.Get("classification").Value.Should().BeSameAs(_catalog.Get("classification").Value);
    }
}
