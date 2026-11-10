using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Barra pedidos que tentam reescrever as regras do assistente ("ignore as regras anteriores"…).
///
/// ISTO É UM FILTRO, NÃO UMA GARANTIA: lista de padrões nunca cobre todas as formas de dizer a
/// mesma coisa. A defesa real é estrutural — o system prompt é fixo e separado, o modelo não tem
/// ferramentas nem acesso a dados além do contexto, e toda minuta passa por revisão humana.
/// </summary>
public static partial class PromptInjectionGuard
{
    private static readonly Regex[] Patterns =
    [
        new(@"\b(ignore|ignorar|ignorem|desconsidere|desconsiderar|esqueca|esquecer|descarte|desobedeca)\b.{0,40}\b(regras?|instrucoes|instrucao|orientacoes|prompt|comandos)\b", Options),
        new(@"\b(ignore|disregard|forget|override|bypass)\b.{0,40}\b(previous|prior|above|earlier|all|your|the)\b.{0,30}\b(instructions?|rules?|prompts?|guidelines?)\b", Options),
        new(@"\b(system prompt|prompt do sistema|prompt de sistema|mensagem de sistema|developer message)\b", Options),
        new(@"\b(revele|mostre|exiba|repita|imprima|reveal|show|print|repeat)\b.{0,30}\b(seu|suas|your|o)\b.{0,20}\b(prompt|instrucoes|instructions|regras)\b", Options),
        new(@"\b(agora voce (e|sera)|a partir de agora voce|finja (ser|que)|aja como|voce agora e|act as|you are now|pretend to be|roleplay as)\b", Options),
        new(@"\b(jailbreak|modo desenvolvedor|developer mode|modo deus|do anything now)\b", Options),
        new(@"<\|?\s*(im_start|im_end|system|assistant|endoftext)\s*\|?>", Options),
        new(@"(^|\n)\s*#{1,6}\s*(system|sistema|assistant|instrucoes)\b", Options),
    ];

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline;

    public static bool Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var folded = RemoveDiacritics(text);

        return Patterns.Any(pattern => pattern.IsMatch(folded));
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
