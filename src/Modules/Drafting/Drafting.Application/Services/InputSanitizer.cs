using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Limpa texto livre antes de persistir ou montar prompt: remove caracteres de controle e
/// invisíveis (usados para esconder instruções), normaliza espaços e impede que o usuário forje os
/// rótulos de seção do user prompt (ex.: uma linha "CONTEXTO RECUPERADO:" falsa).
/// </summary>
public static partial class InputSanitizer
{
    [GeneratedRegex(@"^[ \t]*(TIPO DE PEÇA|ÁREA|FATOS|PEDIDOS|INSTRUÇÕES ADICIONAIS|CONTEXTO RECUPERADO|TAREFA)[ \t]*:", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SectionMarker();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessNewlines();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex ExcessSpaces();

    public static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n").Replace('\r', '\n');

        var builder = new StringBuilder(normalized.Length);
        foreach (var rune in normalized.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            var isNewlineOrTab = rune.Value is 10 or 9;

            // Cc (controle), Cf (formato: zero-width, bidi, BOM), Cs/Co/Cn (surrogate, privado, não atribuído).
            if (!isNewlineOrTab
                && category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
                    or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned)
            {
                continue;
            }

            builder.Append(rune.ToString());
        }

        var text = SectionMarker().Replace(builder.ToString(), match => match.Value.TrimEnd(':').TrimEnd() + " -");
        text = ExcessSpaces().Replace(text, " ");
        text = ExcessNewlines().Replace(text, "\n\n");

        return text.Trim();
    }
}
