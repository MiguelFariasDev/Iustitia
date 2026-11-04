using Advocacia.Modules.Legal.Infrastructure.Services;
using FluentAssertions;

namespace Advocacia.Modules.Legal.Infrastructure.UnitTests.Services;

public class AiPromptExecutorTests
{
    [Fact]
    public void Truncate_ShortContent_IsUntouched()
    {
        AiPromptExecutor.Truncate("abc", 10).Should().Be(("abc", false));
    }

    [Fact]
    public void Truncate_LongContent_CutsAtTheLimit()
    {
        AiPromptExecutor.Truncate("abcdefghij", 4).Should().Be(("abcd", true));
    }

    [Fact]
    public void Truncate_DoesNotSplitASurrogatePair()
    {
        // "😀" ocupa dois chars; cortar entre eles geraria um char inválido no JSON enviado.
        var content = "ab😀cd";

        var (cut, truncated) = AiPromptExecutor.Truncate(content, 3);

        truncated.Should().BeTrue();
        cut.Should().Be("ab");
    }

    [Fact]
    public void Truncate_NonPositiveLimit_DisablesTruncation()
    {
        AiPromptExecutor.Truncate("abcdefghij", 0).Should().Be(("abcdefghij", false));
    }
}
