using Advocacia.Modules.Drafting.Application.Models;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Janela do modelo (lida da seção <c>Ollama</c>). O Ollama TRUNCA EM SILÊNCIO o começo do prompt quando ele
/// não cabe em NumCtx — e o começo é o system prompt. Por isso o tamanho do contexto recuperado é calculado a
/// partir daqui, e não só de um teto fixo.
/// </summary>
public sealed class JanelaModeloOptions
{
    public int NumCtx { get; set; } = 8192;

    public int NumPredict { get; set; } = 2560;
}

public static class OrcamentoContexto
{
    /// <summary>Menos que isto não cabe nem um trecho de cada fonte: melhor recusar do que gerar sem base.</summary>
    public const int MinimoTokens = 1400;

    /// <summary>Rótulos e títulos fixos do user prompt, mais folga para o erro da estimativa.</summary>
    private const int Folga = 350;

    /// <summary>Tokens que sobram para os trechos recuperados.</summary>
    public static int Disponivel(PecaInput input, JanelaModeloOptions janela, double charsPorToken) =>
        Math.Max(0, janela.NumCtx - janela.NumPredict - Folga
            - TokenEstimator.EstimarPrompt(SystemPrompt.Texto, charsPorToken)
            - TokenEstimator.EstimarPrompt(input.Fatos, charsPorToken)
            - TokenEstimator.EstimarPrompt(input.Pedidos, charsPorToken)
            - TokenEstimator.EstimarPrompt(input.InstrucoesAdicionais ?? string.Empty, charsPorToken));

    /// <summary>Quantos caracteres a entrada passa do limite; 0 = cabe.</summary>
    public static int ExcessoEmCaracteres(PecaInput input, JanelaModeloOptions janela, double charsPorToken)
    {
        var falta = MinimoTokens - Disponivel(input, janela, charsPorToken);

        return falta <= 0 ? 0 : (int)Math.Ceiling(falta * charsPorToken);
    }
}
