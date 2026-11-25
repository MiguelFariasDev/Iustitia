using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;

/// <summary>
/// Adaptador entre a porta <see cref="IConsultaProcessoTribunal"/> e o juscraper, que consulta
/// direto os portais públicos dos tribunais (e-SAJ e a nova consulta pública do PJe). Não é
/// registrado sozinho como a porta: quem a atende é <see cref="ConsultaProcessoTribunalComFallback"/>,
/// que usa <see cref="ConsultarDetalhadoAsync"/> para decidir o fallback pela causa da falha.
/// </summary>
public sealed class JuscraperConsultaProcessoTribunal(
    IHttpClientFactory httpClientFactory,
    ILogger<JuscraperConsultaProcessoTribunal> logger) : IConsultaProcessoTribunal
{
    /// <summary>Quantos movimentos seguem para a tela — o mesmo teto do adaptador DataJud.</summary>
    internal const int MaxMovimentos = 10;

    /// <summary>Complemento do cartório acima disto é cortado (com reticências).</summary>
    internal const int MaxComplemento = 300;

    public async Task<Result<ProcessoTribunal?>> ConsultarAsync(
        CNJNumber cnjNumber, CancellationToken cancellationToken = default) =>
        (await ConsultarDetalhadoAsync(cnjNumber, cancellationToken)).Resultado;

    internal async Task<ConsultaJuscraper> ConsultarDetalhadoAsync(CNJNumber cnjNumber, CancellationToken cancellationToken)
    {
        // O juscraper deriva o tribunal do número, como o TribunalResolver. Checar aqui só evita
        // uma chamada de rede para um número que não aponta para lugar nenhum.
        if (cnjNumber.Tribunal is not { } tribunal)
        {
            var erro = ErrorFactory.From(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED, $"{cnjNumber.Segment}.{cnjNumber.Court}");
            return new ConsultaJuscraper(
                Result.Failure<ProcessoTribunal?>(erro), new JuscraperFalha(JuscraperFalha.Codigos.TribunalNaoSuportado, erro));
        }

        var client = new JuscraperClient(httpClientFactory.CreateClient(JuscraperClient.ConsultaHttpClient), logger);

        // max_movimentacoes por instância: os portais devolvem do mais recente para o mais antigo,
        // então 10 de cada instância bastam para os 10 mais recentes no total — e no TJSP isso são
        // 2 requisições ao portal em vez de dezenas.
        var resposta = await client.ConsultarAsync(cnjNumber.Digits, MaxMovimentos, tribunal, cancellationToken);
        if (resposta.Falha is { } falha)
        {
            return new ConsultaJuscraper(Result.Failure<ProcessoTribunal?>(falha.Erro), falha);
        }

        var resultado = resposta.Valor!;
        if (resultado.Avisos is { Count: > 0 } avisos)
        {
            // Resultado parcial (uma instância falhou, a outra respondeu): segue como sucesso. Os
            // avisos são mensagens do serviço sobre a instância, não conteúdo do processo.
            logger.LogWarning("juscraper: resultado parcial para o {Tribunal}: {Avisos}", tribunal, string.Join(" | ", avisos));
        }

        logger.LogInformation(
            "juscraper: {Tribunal} respondeu encontrado={Encontrado} instancias={Instancias}",
            tribunal, resultado.Encontrado, resultado.Processos?.Count ?? 0);

        // Não encontrado é SUCESSO com null (contrato da porta e ADR-064).
        var processo = resultado.Encontrado ? Traduzir(resultado, cnjNumber) : null;

        return new ConsultaJuscraper(Result.Success(processo), null);
    }

    internal static ProcessoTribunal? Traduzir(JuscraperResultado resultado, CNJNumber cnjNumber)
    {
        var processos = resultado.Processos ?? [];
        if (processos.Count == 0)
        {
            return null;
        }

        // Metadados do 1º grau quando houver: o cadastro é do processo de origem (vara, classe,
        // distribuição). Movimentos de TODAS as instâncias: com recurso pendente, o andamento que
        // importa costuma estar no tribunal.
        var principal = processos.FirstOrDefault(p => p.Grau == "G1") ?? processos[0];

        var movimentos = processos
            .SelectMany(p => p.Movimentos ?? [])
            .Where(m => !string.IsNullOrWhiteSpace(m.Nome))
            .OrderByDescending(m => m.DataHora ?? DateTimeOffset.MinValue)
            .Take(MaxMovimentos)
            .Select(m => new MovimentoProcesso(NomeComComplemento(m.Nome!, m.Complemento), m.DataHora))
            .ToArray();

        return new ProcessoTribunal(
            // Número MASCARADO: forma canônica do sistema e o que a tela exibe.
            cnjNumber.Value,
            principal.Tribunal ?? cnjNumber.Tribunal ?? string.Empty,
            principal.Grau,
            principal.DataAjuizamento,
            processos.Any(p => p.EmSegredoDeJustica),
            principal.Classe,
            principal.Sistema,
            principal.OrgaoJulgador,
            principal.Assuntos ?? [],
            movimentos,
            principal.CodigoMunicipioIbge,
            FonteConsulta.Juscraper);
    }

    /// <summary>
    /// No e-SAJ, o título do movimento ("Expedido Certidão") diz pouco; o complemento que o cartório
    /// escreve diz o resto. Como <see cref="MovimentoProcesso"/> só tem Nome, os dois vão juntos.
    /// </summary>
    internal static string NomeComComplemento(string nome, string? complemento)
    {
        if (string.IsNullOrWhiteSpace(complemento) || complemento.Trim().Equals(nome.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return nome;
        }

        var texto = complemento.Trim();
        if (texto.Length > MaxComplemento)
        {
            texto = texto[..MaxComplemento].TrimEnd() + "…";
        }

        return $"{nome} — {texto}";
    }
}

/// <summary>Resultado da porta mais a falha original do juscraper (nula no sucesso).</summary>
internal sealed record ConsultaJuscraper(Result<ProcessoTribunal?> Resultado, JuscraperFalha? Falha);
