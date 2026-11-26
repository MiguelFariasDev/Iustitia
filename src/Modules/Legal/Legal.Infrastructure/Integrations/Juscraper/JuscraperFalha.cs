using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;

/// <summary>
/// Falha do juscraper com o código ORIGINAL preservado. O <see cref="Erro"/> do catálogo junta
/// causas diferentes (CNJ_TRIBUNAL_NOT_SUPPORTED vale para "sem scraper", "captcha" e "bloqueio"),
/// e a decisão de fallback precisa da causa — não da mensagem.
/// </summary>
/// <param name="Motivo">Código do juscraper (ex.: CAPTCHA_EXIGIDO) ou um dos <see cref="Motivos"/> locais.</param>
/// <param name="Erro">Erro do catálogo do Iustitia correspondente.</param>
internal sealed record JuscraperFalha(string Motivo, Error Erro)
{
    /// <summary>Tribunal que o juscraper não consegue consultar de forma permanente.</summary>
    public bool SemCobertura => Motivo is Codigos.TribunalNaoSuportado or Codigos.CaptchaExigido or Codigos.AcessoBloqueado;

    /// <summary>Falha que tende a passar sozinha (portal ou serviço fora do ar, rede, timeout).</summary>
    public bool Transitoria => Motivo is Codigos.TribunalIndisponivel or Motivos.Timeout or Motivos.Rede
        or Motivos.CircuitoAberto or Motivos.ErroDoServidor or Motivos.LimiteExcedido;

    public bool NumeroInvalido => Motivo == Codigos.NumeroCnjInvalido;

    public bool NaoAutorizado => Motivo == Codigos.NaoAutorizado;

    /// <summary>Códigos do ProblemDetails do juscraper (seção 4 do contrato).</summary>
    internal static class Codigos
    {
        public const string NumeroCnjInvalido = "NUMERO_CNJ_INVALIDO";
        public const string TribunalNaoSuportado = "TRIBUNAL_NAO_SUPORTADO";
        public const string CaptchaExigido = "CAPTCHA_EXIGIDO";
        public const string AcessoBloqueado = "ACESSO_BLOQUEADO";
        public const string TribunalIndisponivel = "TRIBUNAL_INDISPONIVEL";
        public const string RespostaInesperada = "RESPOSTA_INESPERADA";
        public const string NaoAutorizado = "NAO_AUTORIZADO";
    }

    /// <summary>Causas sem ProblemDetails, detectadas no Iustitia. Também vão como tag de métrica.</summary>
    internal static class Motivos
    {
        public const string Timeout = "timeout";
        public const string Rede = "rede";
        public const string CircuitoAberto = "circuito_aberto";
        public const string ErroDoServidor = "erro_servidor";
        public const string LimiteExcedido = "limite_excedido";
        public const string RespostaIlegivel = "resposta_ilegivel";
        public const string NaoEncontrado = "nao_encontrado";
        public const string Desligado = "juscraper_desligado";
    }
}

/// <summary>Valor OU falha de uma chamada ao juscraper.</summary>
internal sealed record JuscraperResposta<T>(T? Valor, JuscraperFalha? Falha)
    where T : class
{
    public static JuscraperResposta<T> Ok(T valor) => new(valor, null);

    public static JuscraperResposta<T> Falhou(JuscraperFalha falha) => new(null, falha);
}
