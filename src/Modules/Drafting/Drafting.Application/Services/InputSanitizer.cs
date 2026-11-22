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
    [GeneratedRegex(@"^[ \t]*(TIPO DE PEÇA|ÁREA|FATOS(?: FORNECIDOS PELA ADVOGADA)?|PEDIDOS(?: SOLICITADOS)?|INSTRUÇÕES ADICIONAIS|DOCUMENTOS DO CASO|CONTEXTO (?:JURÍDICO )?RECUPERADO|TAREFA|REGRAS ABSOLUTAS)[ \t]*:", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
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

        var text = NeutralizarMarcadores(RemoverInvisiveis(value));
        text = ExcessSpaces().Replace(text, " ");
        text = ExcessNewlines().Replace(text, "\n\n");

        return text.Trim();
    }

    /// <summary>Uma linha "TAREFA:" vinda de fora vira "TAREFA -": não abre seção falsa no prompt.</summary>
    public static string NeutralizarMarcadores(string texto) =>
        SectionMarker().Replace(texto, match => match.Value.TrimEnd(':').TrimEnd() + " -");

    /// <summary>
    /// Quebras de linha uniformes e sem caracteres de controle ou invisíveis. Preserva a estrutura: é o que se
    /// aplica ao texto de documentos (o Markdown guardado não pode ter as seções do original alteradas).
    /// </summary>
    public static string RemoverInvisiveis(string value)
    {
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

        return builder.ToString();
    }
}
