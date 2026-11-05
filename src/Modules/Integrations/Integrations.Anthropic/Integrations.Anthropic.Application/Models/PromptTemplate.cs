using System.Text.RegularExpressions;

namespace Advocacia.Modules.Integrations.Anthropic.Application.Models;

/// <summary>
/// Prompt versionado: system prompt fixo + template de usuário com placeholders
/// <c>{nome}</c>. A <see cref="Version"/> é gravada junto de cada sugestão, para saber qual
/// texto produziu qual resposta quando o prompt evoluir.
/// </summary>
public sealed partial record PromptTemplate(string Name, string Version, string SystemPrompt, string UserTemplate)
{
    /// <summary>
    /// Substitui os placeholders em UMA passada. Substituição sequencial (Replace um a um)
    /// permitiria que o texto de uma publicação contendo "{area}" fosse reinterpretado
    /// como placeholder — a passada única trata o valor inserido como dado, nunca como
    /// template. Placeholder sem valor informado é mantido literal.
    /// </summary>
    public string RenderUser(IReadOnlyDictionary<string, string> values) =>
        PlaceholderRegex().Replace(
            UserTemplate,
            match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);

    [GeneratedRegex(@"\{([a-z_]+)\}")]
    private static partial Regex PlaceholderRegex();
}
