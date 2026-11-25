using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Legal.Infrastructure.Integrations.Acompanhamento;

/// <summary>
/// Andamentos pelo juscraper (GET /v1/processos/{numero}/movimentacoes). O id de cada andamento é o
/// do juscraper — SHA-256 estável entre consultas, com ou sem <c>desde</c>. Andamento sem id é
/// descartado aqui mesmo.
/// </summary>
public sealed class JuscraperAcompanhamento(
    IHttpClientFactory httpClientFactory,
    ILogger<JuscraperAcompanhamento> logger)
{
    internal async Task<(Result<AndamentosCapturados> Resultado, JuscraperFalha? Falha)> ObterAsync(
        CNJNumber cnjNumber, DateOnly? desde, CancellationToken cancellationToken)
    {
        if (cnjNumber.Tribunal is not { } tribunal)
        {
            var erro = ErrorFactory.From(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED, $"{cnjNumber.Segment}.{cnjNumber.Court}");
            return (Result.Failure<AndamentosCapturados>(erro), new JuscraperFalha(JuscraperFalha.Codigos.TribunalNaoSuportado, erro));
        }

        var client = new JuscraperClient(httpClientFactory.CreateClient(JuscraperClient.AcompanhamentoHttpClient), logger);
        var resposta = await client.MovimentacoesAsync(cnjNumber.Digits, desde, tribunal, cancellationToken);
        if (resposta.Falha is { } falha)
        {
            return (Result.Failure<AndamentosCapturados>(falha.Erro), falha);
        }

        var movimentacoes = resposta.Valor!;
        if (movimentacoes.Avisos is { Count: > 0 } avisos)
        {
            logger.LogWarning("juscraper: acompanhamento parcial no {Tribunal}: {Avisos}", tribunal, string.Join(" | ", avisos));
        }

        if (!movimentacoes.Completas)
        {
            logger.LogWarning("juscraper: lista de andamentos truncada pelo serviço no {Tribunal}.", tribunal);
        }

        var andamentos = (movimentacoes.Movimentos ?? [])
            .Where(m => !string.IsNullOrWhiteSpace(m.Id) && !string.IsNullOrWhiteSpace(m.Nome))
            .Select(m => new AndamentoCapturado(m.Id, m.Nome!.Trim(), m.Complemento, m.DataHora, m.Instancia, m.PossuiDocumento))
            .ToList();

        return (Result.Success(new AndamentosCapturados(
            movimentacoes.Encontrado, FonteConsulta.Juscraper, movimentacoes.Completas, andamentos)), null);
    }
}
