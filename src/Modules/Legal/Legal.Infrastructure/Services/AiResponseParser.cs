using System.Text.Json;
using Advocacia.Modules.Legal.Application.Ai;
using Advocacia.Modules.Legal.Domain.Suggestions;

namespace Advocacia.Modules.Legal.Infrastructure.Services;

/// <summary>Resultado do parse: o valor validado OU o motivo (texto fixo, nunca conteúdo do modelo).</summary>
public sealed record ParseOutcome<T>(T? Value, string? Reason)
    where T : class
{
    public bool IsValid => Value is not null;

    public static ParseOutcome<T> Ok(T value) => new(value, null);

    public static ParseOutcome<T> Invalid(string reason) => new(null, reason);
}

public sealed record ParsedClassification(
    string ActType, int? DeadlineDays, DeadlineType? DeadlineType, string? LegalBasis, int Confidence, string Json);

public sealed record ParsedDeadline(
    int? Days, DeadlineType? Type, string? LegalBasis, int Confidence, string Json);

/// <summary>
/// Validador do JSON devolvido pelo modelo (System.Text.Json). O modelo é uma fonte NÃO
/// confiável: nada que ele devolva chega ao banco sem passar por aqui. Rejeita — em vez de
/// "consertar" — JSON malformado, campo ausente, tipo errado, act_type fora da lista fechada,
/// prazo fora de 0–365, confiança fora de 0–100 e prazo sem forma de contagem (ou vice-versa).
///
/// Os motivos de rejeição são strings fixas que citam só o NOME do campo: o texto devolvido
/// pelo modelo pode repetir trechos da publicação e por isso jamais vai para log ou erro.
/// </summary>
public static class AiResponseParser
{
    private const int MaxLegalBasisLength = 2000;

    public static ParseOutcome<ParsedClassification> ParseClassification(string? text)
    {
        var root = ParseRoot(text, out var json, out var rootReason);
        if (root is null)
        {
            return ParseOutcome<ParsedClassification>.Invalid(rootReason!);
        }

        using (root)
        {
            var element = root.RootElement;

            if (!TryGetString(element, "act_type", required: true, out var actType, out var reason))
            {
                return ParseOutcome<ParsedClassification>.Invalid(reason!);
            }

            if (!ActTypes.All.Contains(actType!))
            {
                return ParseOutcome<ParsedClassification>.Invalid("campo 'act_type' fora da lista permitida");
            }

            var common = ParseCommon(element);
            if (common.Reason is not null)
            {
                return ParseOutcome<ParsedClassification>.Invalid(common.Reason);
            }

            return ParseOutcome<ParsedClassification>.Ok(new ParsedClassification(
                actType!, common.Days, common.Type, common.LegalBasis, common.Confidence, json!));
        }
    }

    public static ParseOutcome<ParsedDeadline> ParseDeadline(string? text)
    {
        var root = ParseRoot(text, out var json, out var rootReason);
        if (root is null)
        {
            return ParseOutcome<ParsedDeadline>.Invalid(rootReason!);
        }

        using (root)
        {
            var common = ParseCommon(root.RootElement);
            if (common.Reason is not null)
            {
                return ParseOutcome<ParsedDeadline>.Invalid(common.Reason);
            }

            return ParseOutcome<ParsedDeadline>.Ok(new ParsedDeadline(
                common.Days, common.Type, common.LegalBasis, common.Confidence, json!));
        }
    }

    private static (int? Days, DeadlineType? Type, string? LegalBasis, int Confidence, string? Reason) ParseCommon(
        JsonElement element)
    {
        if (!TryGetOptionalInt(element, "deadline_days", out var days, out var reason))
        {
            return (null, null, null, 0, reason);
        }

        if (days is < 0 or > PublicationSuggestion.MaxDeadlineDays)
        {
            return (null, null, null, 0, "campo 'deadline_days' fora do intervalo permitido");
        }

        if (!TryGetString(element, "deadline_type", required: false, out var typeText, out reason))
        {
            return (null, null, null, 0, reason);
        }

        DeadlineType? type = null;
        if (typeText is not null)
        {
            if (!DeadlineTypeExtensions.TryParse(typeText, out var parsedType))
            {
                return (null, null, null, 0, "campo 'deadline_type' com valor desconhecido");
            }

            type = parsedType;
        }

        // Prazo e forma de contagem andam juntos (mesma invariante da entidade).
        if (days.HasValue != type.HasValue)
        {
            return (null, null, null, 0, "campos 'deadline_days' e 'deadline_type' inconsistentes");
        }

        if (!TryGetString(element, "legal_basis", required: false, out var legalBasis, out reason))
        {
            return (null, null, null, 0, reason);
        }

        if (legalBasis is { Length: > MaxLegalBasisLength })
        {
            return (null, null, null, 0, "campo 'legal_basis' excede o tamanho máximo");
        }

        if (!TryGetRequiredInt(element, "confidence", out var confidence, out reason))
        {
            return (null, null, null, 0, reason);
        }

        if (confidence is < 0 or > 100)
        {
            return (null, null, null, 0, "campo 'confidence' fora do intervalo 0-100");
        }

        return (days, type, legalBasis, confidence, null);
    }

    /// <summary>
    /// Aceita o objeto JSON puro ou embrulhado numa cerca markdown (```json ... ```), que os
    /// modelos às vezes adicionam apesar da instrução. Qualquer outro texto em volta é rejeitado.
    /// </summary>
    private static JsonDocument? ParseRoot(string? text, out string? json, out string? reason)
    {
        json = null;
        reason = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            reason = "resposta vazia";
            return null;
        }

        var candidate = StripCodeFence(text.Trim());

        try
        {
            var document = JsonDocument.Parse(candidate);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                reason = "raiz do JSON não é um objeto";
                return null;
            }

            json = document.RootElement.GetRawText();
            return document;
        }
        catch (JsonException)
        {
            reason = "JSON malformado";
            return null;
        }
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal) || !text.EndsWith("```", StringComparison.Ordinal)
            || text.Length < 6)
        {
            return text;
        }

        var inner = text[3..^3];
        var firstNewLine = inner.IndexOf('\n');

        // Descarta a linha do rótulo da cerca ("json").
        return firstNewLine >= 0 ? inner[(firstNewLine + 1)..].Trim() : inner.Trim();
    }

    private static bool TryGetString(
        JsonElement element, string name, bool required, out string? value, out string? reason)
    {
        value = null;
        reason = null;

        if (!element.TryGetProperty(name, out var property))
        {
            if (required)
            {
                reason = $"campo '{name}' ausente";
                return false;
            }

            return true;
        }

        switch (property.ValueKind)
        {
            case JsonValueKind.Null when !required:
                return true;
            case JsonValueKind.String:
                var text = property.GetString()?.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    if (required)
                    {
                        reason = $"campo '{name}' vazio";
                        return false;
                    }

                    return true;
                }

                value = text;
                return true;
            default:
                reason = $"campo '{name}' com tipo inválido";
                return false;
        }
    }

    private static bool TryGetOptionalInt(JsonElement element, string name, out int? value, out string? reason)
    {
        value = null;
        reason = null;

        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            value = number;
            return true;
        }

        reason = $"campo '{name}' com tipo inválido";
        return false;
    }

    private static bool TryGetRequiredInt(JsonElement element, string name, out int value, out string? reason)
    {
        value = 0;
        reason = null;

        if (!element.TryGetProperty(name, out var property))
        {
            reason = $"campo '{name}' ausente";
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
        {
            return true;
        }

        reason = $"campo '{name}' com tipo inválido";
        return false;
    }
}
