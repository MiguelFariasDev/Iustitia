namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>Estimativa de tokens por comprimento do texto (o tokenizador real não está disponível aqui).</summary>
public static class TokenEstimator
{
    public static int Estimate(string text, double charsPorToken)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return (int)Math.Ceiling(text.Length / Math.Max(1.0, charsPorToken));
    }

    /// <summary>
    /// Estimativa para o ORÇAMENTO do prompt, onde errar para menos faz o Ollama cortar o system prompt. No
    /// tokenizador do Qwen2.5 (base do Jurema-7B) cada dígito é um token e a pontuação quase sempre também, por
    /// isso texto jurídico rende ~2,5 caracteres por token, não 3,5. Só letras e espaços usam a razão. Medido em
    /// 1.168 trechos do acervo: com 3,3 subestima 9% deles, em até ~10%; a razão simples de 3,5 subestimava 84%.
    /// </summary>
    public static int EstimarPrompt(string text, double letrasPorToken)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var letras = 0;
        var tokens = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsDigit(c))
            {
                tokens++;
            }
            else if (char.IsLetter(c) || c == ' ')
            {
                letras++;
            }
            else if (i == 0 || text[i - 1] != c)
            {
                // Repetição ("-----", linhas em branco) é um token só.
                tokens++;
            }
        }

        return tokens + (int)Math.Ceiling(letras / Math.Max(1.0, letrasPorToken));
    }
}
