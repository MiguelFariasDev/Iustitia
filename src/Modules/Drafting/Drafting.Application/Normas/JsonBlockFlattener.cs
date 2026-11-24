using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Advocacia.Modules.Drafting.Application.Normas;

/// <summary>
/// Parte da jurisprudência do acervo (Banco Nacional de Precedentes) vem como um bloco ```json com o
/// registro bruto: ids, links e datas que só poluem a busca vetorial. Aqui o bloco vira linhas
/// "Campo: texto" apenas com os campos textuais longos (ementa, decisão, tese…).
/// </summary>
public static partial class JsonBlockFlattener
{
    private const int MinCharsTexto = 40;
    private const int MaxCharsBloco = 400_000;

    private static readonly string[] CamposIgnorados = ["textoato", "link", "url", "hash", "uri"];

    public static string Flatten(string markdown) =>
        FencedJson().Replace(markdown, match => FlattenBlock(match.Groups[1].Value));

    private static string FlattenBlock(string json)
    {
        if (json.Length > MaxCharsBloco)
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var sb = new StringBuilder();
            Walk(document.RootElement, null, sb);

            return sb.ToString();
        }
        catch (JsonException)
        {
            // JSON quebrado: melhor descartar o ruído do que indexar chaves e chaves.
            return string.Empty;
        }
    }

    private static void Walk(JsonElement element, string? name, StringBuilder sb)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, property.Name, sb);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, name, sb);
                }

                break;
            case JsonValueKind.String:
                var text = element.GetString();
                if (name is not null && text is { Length: >= MinCharsTexto } && !Ignorado(name))
                {
                    sb.Append(Humanizar(name)).Append(": ").Append(text.Trim()).Append("\n\n");
                }

                break;
        }
    }

    private static bool Ignorado(string name) =>
        CamposIgnorados.Any(prefix => name.Contains(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>"textoEmenta" → "Texto ementa".</summary>
    private static string Humanizar(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name)
        {
            if (char.IsUpper(ch) && sb.Length > 0)
            {
                sb.Append(' ').Append(char.ToLowerInvariant(ch));
            }
            else
            {
                sb.Append(sb.Length == 0 ? char.ToUpperInvariant(ch) : ch);
            }
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"```json\s*\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex FencedJson();
}
