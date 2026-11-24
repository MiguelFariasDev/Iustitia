namespace Advocacia.Modules.Legal.Domain.Prazos;

public enum TipoContagem
{
    /// <summary>Dias úteis (CPC art. 219): regra geral dos prazos processuais.</summary>
    DiasUteis,

    /// <summary>Dias corridos: prazos materiais e os que a lei ou o juiz fixaram em dias corridos.</summary>
    DiasCorridos,
}

public enum ReferenciaDoPrazo
{
    /// <summary>A data informada é a da ciência/intimação pessoal: a contagem começa no 1º dia útil seguinte.</summary>
    Ciencia,

    /// <summary>
    /// A data informada é a da DISPONIBILIZAÇÃO no Diário Eletrônico: a publicação é o 1º dia útil seguinte
    /// (Lei 11.419/2006, art. 4º §3º) e a contagem começa no 1º dia útil depois dela (CPC art. 224 §3º).
    /// </summary>
    Disponibilizacao,
}

public sealed record ResultadoPrazo(
    DateOnly Vencimento,
    DateOnly InicioDaContagem,
    DateOnly? DataDaPublicacao,
    int DiasContados,
    int DiasDeRecessoIgnorados,
    IReadOnlyList<string> Observacoes);

/// <summary>
/// Calculadora de prazo processual — DETERMINÍSTICA, sem IA. Segue o CPC: exclui o dia do começo e inclui o
/// do vencimento (art. 224), conta só dias úteis para prazos processuais (art. 219), suspende o curso no
/// recesso de 20/12 a 20/01 (art. 220) e prorroga o vencimento que cair em dia sem expediente (art. 224 §1º).
/// É uma ESTIMATIVA: só conhece o calendário nacional; o que for local entra em <c>feriadosLocais</c>.
/// </summary>
public static class CalculadoraPrazo
{
    public const int MaxDias = 1000;

    /// <exception cref="ArgumentOutOfRangeException">Dias fora de 1..<see cref="MaxDias"/>.</exception>
    public static ResultadoPrazo Calcular(
        DateOnly dataBase,
        int dias,
        TipoContagem tipo = TipoContagem.DiasUteis,
        ReferenciaDoPrazo referencia = ReferenciaDoPrazo.Ciencia,
        IReadOnlySet<DateOnly>? feriadosLocais = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dias, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dias, MaxDias);

        var observacoes = new List<string>();
        DateOnly? publicacao = null;
        var referenciaDaContagem = dataBase;
        if (referencia == ReferenciaDoPrazo.Disponibilizacao)
        {
            publicacao = CalendarioForense.ProximoDiaUtil(dataBase, feriadosLocais);
            referenciaDaContagem = publicacao.Value;
            observacoes.Add($"Publicação considerada em {publicacao:dd/MM/yyyy} (1º dia útil após a disponibilização).");
        }

        // O dia do começo não conta: a contagem inicia no 1º dia ÚTIL seguinte (CPC art. 224 §3º).
        var inicio = CalendarioForense.ProximoDiaUtil(referenciaDaContagem, feriadosLocais);

        var contados = 0;
        var recesso = 0;
        var dia = inicio;
        var ultimo = inicio;
        while (contados < dias)
        {
            if (CalendarioForense.EmRecesso(dia))
            {
                recesso++;
            }
            else if (tipo == TipoContagem.DiasCorridos || CalendarioForense.DiaUtil(dia, feriadosLocais))
            {
                contados++;
                ultimo = dia;
            }

            if (contados < dias)
            {
                dia = dia.AddDays(1);
            }
        }

        var vencimento = ultimo;
        if (!CalendarioForense.DiaUtil(vencimento, feriadosLocais))
        {
            var original = vencimento;
            vencimento = CalendarioForense.ProximoDiaUtil(vencimento.AddDays(-1), feriadosLocais);
            // O próximo dia útil pode cair no recesso: o expediente só volta depois de 20/01.
            while (CalendarioForense.EmRecesso(vencimento))
            {
                vencimento = CalendarioForense.ProximoDiaUtil(vencimento, feriadosLocais);
            }

            observacoes.Add($"Vencimento em {original:dd/MM/yyyy} (dia sem expediente) prorrogado para {vencimento:dd/MM/yyyy} (CPC art. 224 §1º).");
        }

        if (recesso > 0)
        {
            observacoes.Add($"O recesso forense (20/12 a 20/01) suspendeu a contagem por {recesso} dia(s) (CPC art. 220).");
        }

        observacoes.Add("Estimativa pelo calendário NACIONAL. Confira feriados locais, suspensões e o prazo no processo antes de confiar.");

        return new ResultadoPrazo(vencimento, inicio, publicacao, contados, recesso, observacoes);
    }
}
