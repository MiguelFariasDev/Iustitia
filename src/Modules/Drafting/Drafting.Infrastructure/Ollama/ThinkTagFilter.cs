using System.Text;

namespace Advocacia.Modules.Drafting.Infrastructure.Ollama;

/// <summary>
/// Remove o bloco &lt;think&gt;…&lt;/think&gt; que modelos de raciocínio (qwen3) podem emitir no
/// meio do texto, mesmo com <c>think:false</c> em versões antigas do Ollama. Funciona em fluxo: a
/// tag pode chegar partida entre dois fragmentos ("&lt;thi" + "nk&gt;"). Também descarta os
/// espaços em branco que o modelo deixa depois do bloco, para a minuta não começar com linhas vazias.
/// </summary>
public sealed class ThinkTagFilter
{
    private const string Open = "<think>";
    private const string Close = "</think>";

    private readonly StringBuilder _pending = new();
    private bool _inside;
    private bool _started;

    public string Push(string piece)
    {
        _pending.Append(piece);
        var output = new StringBuilder();

        while (true)
        {
            var text = _pending.ToString();

            if (!_inside)
            {
                var open = text.IndexOf(Open, StringComparison.Ordinal);
                if (open >= 0)
                {
                    output.Append(text, 0, open);
                    _pending.Clear().Append(text, open + Open.Length, text.Length - open - Open.Length);
                    _inside = true;
                    continue;
                }

                var keep = PartialSuffix(text, Open);
                output.Append(text, 0, text.Length - keep);
                _pending.Clear().Append(text, text.Length - keep, keep);
                break;
            }

            var close = text.IndexOf(Close, StringComparison.Ordinal);
            if (close >= 0)
            {
                _pending.Clear().Append(text, close + Close.Length, text.Length - close - Close.Length);
                _inside = false;
                continue;
            }

            // Ainda dentro do raciocínio: descarta, guardando só um possível começo de "</think>".
            var held = PartialSuffix(text, Close);
            _pending.Clear().Append(text, text.Length - held, held);
            break;
        }

        return TrimLeading(output.ToString());
    }

    /// <summary>Fim do fluxo: devolve o que ficou retido (uma tag aberta e nunca fechada é descartada).</summary>
    public string Flush()
    {
        if (_inside)
        {
            _pending.Clear();

            return string.Empty;
        }

        var rest = _pending.ToString();
        _pending.Clear();

        return TrimLeading(rest);
    }

    private string TrimLeading(string text)
    {
        if (_started || text.Length == 0)
        {
            return text;
        }

        var trimmed = text.TrimStart();
        _started = trimmed.Length > 0;

        return trimmed;
    }

    private static int PartialSuffix(string text, string tag)
    {
        for (var length = Math.Min(tag.Length - 1, text.Length); length > 0; length--)
        {
            if (text.EndsWith(tag[..length], StringComparison.Ordinal))
            {
                return length;
            }
        }

        return 0;
    }
}
