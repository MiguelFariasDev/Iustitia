using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using FluentAssertions;

namespace Advocacia.Modules.Integrations.Anthropic.UnitTests;

public class PromptTemplateTests
{
    private static PromptTemplate Template(string user) => new("t", "1.0.0", "system", user);

    [Fact]
    public void RenderUser_ReplacesAllPlaceholders()
    {
        var rendered = Template("{tribunal}/{area}: {raw_content}").RenderUser(
            new Dictionary<string, string> { ["tribunal"] = "TJCE", ["area"] = "Civel", ["raw_content"] = "texto" });

        rendered.Should().Be("TJCE/Civel: texto");
    }

    [Fact]
    public void RenderUser_InsertedValueIsDataNotTemplate()
    {
        // O texto da publicação contém "{area}": não pode ser reinterpretado como placeholder.
        var rendered = Template("{raw_content} | {area}").RenderUser(
            new Dictionary<string, string> { ["raw_content"] = "veja {area}", ["area"] = "Civel" });

        rendered.Should().Be("veja {area} | Civel");
    }

    [Fact]
    public void RenderUser_MissingValue_KeepsPlaceholderLiteral()
    {
        Template("{tribunal}").RenderUser(new Dictionary<string, string>()).Should().Be("{tribunal}");
    }
}
