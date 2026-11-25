using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Legal.Infrastructure.Integrations.Acompanhamento;

/// <summary>
/// Fonte do acompanhamento por processo (ADR-075, 6.4). Diferente da consulta do cadastro: misturar
/// fontes no mesmo processo DUPLICARIA andamentos (o DataJud usa os nomes da Tabela Unificada e
/// timestamps; o portal, os nomes do cartório, às vezes só com a data — ids diferentes para o mesmo
/// evento). Por isso:
/// <list type="bullet">
/// <item>processo já acompanhado numa fonte continua nela (troca só explícita, pelo comando);</item>
/// <item>primeira vez: juscraper; DataJud só se o juscraper não cobrir o tribunal de forma
/// PERMANENTE (sem scraper, captcha, bloqueio);</item>
/// <item>erro transitório (portal fora, timeout, rede): sem fallback — pula esta noite, a próxima retenta.</item>
/// </list>
/// </summary>
public sealed class AcompanhamentoProcessualComFallback(
    JuscraperAcompanhamento juscraper,
    DataJudAcompanhamento dataJud,
    IOptions<JuscraperOptions> options,
    ILogger<AcompanhamentoProcessualComFallback> logger) : IAcompanhamentoProcessual
{
    public async Task<Result<AndamentosCapturados>> ObterAsync(
        CNJNumber cnjNumber, string? fonte, DateOnly? desde, CancellationToken cancellationToken = default)
    {
        var config = options.Value;

        if (fonte == FonteConsulta.DataJud)
        {
            return config.FallbackDataJud
                ? await dataJud.ObterAsync(cnjNumber, desde, cancellationToken)
                : SemFonte(cnjNumber);
        }

        if (!config.Habilitado)
        {
            // Juscraper desligado. Processo que já está nele espera (não troca de fonte sozinho).
            if (fonte == FonteConsulta.Juscraper)
            {
                return Result.Failure<AndamentosCapturados>(ErrorFactory.From(ErrorCode.CNJ_UNAVAILABLE));
            }

            return config.FallbackDataJud ? await dataJud.ObterAsync(cnjNumber, desde, cancellationToken) : SemFonte(cnjNumber);
        }

        var (resultado, falha) = await juscraper.ObterAsync(cnjNumber, desde, cancellationToken);
        if (falha is null || fonte == FonteConsulta.Juscraper)
        {
            return resultado;
        }

        if (falha.SemCobertura && config.FallbackDataJud)
        {
            logger.LogInformation(
                "Acompanhamento do {Tribunal} pelo DataJud: juscraper sem cobertura ({Motivo}).", cnjNumber.Tribunal, falha.Motivo);
            return await dataJud.ObterAsync(cnjNumber, desde, cancellationToken);
        }

        return resultado;
    }

    /// <summary>Nenhuma fonte permitida cobre o tribunal: o job conta como "sem cobertura".</summary>
    private static Result<AndamentosCapturados> SemFonte(CNJNumber cnjNumber) =>
        Result.Failure<AndamentosCapturados>(
            ErrorFactory.From(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED, cnjNumber.Tribunal ?? $"{cnjNumber.Segment}.{cnjNumber.Court}"));
}
