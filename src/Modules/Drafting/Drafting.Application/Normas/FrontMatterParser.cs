using System.Text;

namespace Advocacia.Modules.Drafting.Application.Normas;

public sealed record FrontMatter(IReadOnlyDictionary<string, string?> Valores, string Corpo, bool Presente)
{
    public string? Get(string chave) => Valores.TryGetValue(chave, out var valor) ? valor : null;
}

/// <summary>
/// Lê o cabeçalho YAML dos Markdowns do acervo. O formato é PLANO (chave: valor, escalares entre aspas ou
/// não, <c>null</c>, linhas de continuação indentadas) — um parser completo de YAML seria dependência e
/// superfície de ataque (âncoras, tags, objetos) sem ganho. O que não for reconhecido é ignorado, nunca
/// executado. Não confie no resultado: quem consome valida (ver <see cref="NormaMetadataResolver"/>).
/// </summary>
public static class FrontMatterParser
{
    private const int MaxBytesCabecalho = 64 * 1024;

    public static FrontMatter Parse(string markdown)
    {
        var text = markdown.TrimStart('﻿');
        var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            return new FrontMatter(valores, text, false);
        }

        // A abertura é "---" sozinho na linha; o fechamento é a próxima linha "---".
        var firstEnd = text.IndexOf('\n');
        if (firstEnd < 0 || text[..firstEnd].Trim() != "---")
        {
            return new FrontMatter(valores, text, false);
        }

        var lines = new List<string>();
        var position = firstEnd + 1;
        var closed = false;
        while (position < text.Length && position - firstEnd < MaxBytesCabecalho)
        {
            var end = text.IndexOf('\n', position);
            var line = (end < 0 ? text[position..] : text[position..end]).TrimEnd('\r');
            position = end < 0 ? text.Length : end + 1;
            if (line.Trim() == "---")
            {
                closed = true;
                break;
            }

            lines.Add(line);
        }

        if (!closed)
        {
            // Sem fechamento não dá para separar metadado de conteúdo: trata tudo como corpo.
            return new FrontMatter(valores, text, false);
        }

        string? currentKey = null;
        var buffer = new StringBuilder();
        void Flush()
        {
            if (currentKey is not null)
            {
                valores[currentKey] = Unquote(buffer.ToString().Trim());
            }

            buffer.Clear();
        }

        foreach (var line in lines)
        {
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            if (char.IsWhiteSpace(line[0]))
            {
                // Continuação do valor anterior (texto dobrado).
                if (currentKey is not null)
                {
                    buffer.Append(' ').Append(line.Trim());
                }

                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            Flush();
            currentKey = line[..colon].Trim();
            buffer.Append(line[(colon + 1)..].Trim());
        }

        Flush();

        return new FrontMatter(valores, text[position..].TrimStart('\r', '\n'), true);
    }

    private static string? Unquote(string raw)
    {
        if (raw.Length == 0 || raw is "null" or "~" or "Null" or "NULL")
        {
            return null;
        }

        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            return raw[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", " ");
        }

        if (raw.Length >= 2 && raw[0] == '\'' && raw[^1] == '\'')
        {
            return raw[1..^1].Replace("''", "'");
        }

        return raw;
    }
}
