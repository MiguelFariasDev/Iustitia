using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Shared;
using Advocacia.Modules.Legal.Infrastructure.Integrations.Juscraper;
using Advocacia.Modules.Legal.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Legal.Infrastructure.Integrations;

/// <summary>
/// Implementação registrada da porta <see cref="IConsultaProcessoTribunal"/>: juscraper como fonte
/// principal e DataJud como fallback (ADR-075). Matriz de decisão:
/// <list type="bullet">
/// <item>juscraper encontrou → juscraper;</item>
/// <item>juscraper "não encontrado" → não encontrado (o portal é a fonte primária), salvo
/// <c>FallbackQuandoNaoEncontrado</c>;</item>
/// <item>sem cobertura (sem scraper, captcha, bloqueio), portal fora do ar, layout novo, timeout,
/// rede, 5xx, circuito aberto ou chave recusada → DataJud;</item>
/// <item>número inválido → erro, sem fallback (inválido para as duas fontes);</item>
/// <item><c>Juscraper:BaseUrl</c> vazio → só DataJud (comportamento anterior).</item>
/// </list>
/// Se o DataJud também falhar, vale o erro dele (última fonte tentada), e os dois códigos vão para o log.
/// </summary>
public sealed class ConsultaProcessoTribunalComFallback(
    JuscraperConsultaProcessoTribunal juscraper,
    [FromKeyedServices(FonteConsulta.DataJud)] IConsultaProcessoTribunal dataJud,
    IOptions<JuscraperOptions> options,
    ConsultaTribunalTelemetry telemetry,
    ILogger<ConsultaProcessoTribunalComFallback> logger) : IConsultaProcessoTribunal
{
    public async Task<Result<ProcessoTribunal?>> ConsultarAsync(
        CNJNumber cnjNumber, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        var tribunal = cnjNumber.Tribunal;

        if (!config.Habilitado)
        {
            telemetry.Registrar(FonteConsulta.DataJud, tribunal, JuscraperFalha.Motivos.Desligado);
            return await dataJud.ConsultarAsync(cnjNumber, cancellationToken);
        }

        var (resultado, falha) = await juscraper.ConsultarDetalhadoAsync(cnjNumber, cancellationToken);

        string motivo;
        if (falha is null)
        {
            if (resultado.Value is not null || !config.FallbackQuandoNaoEncontrado)
            {
                telemetry.Registrar(FonteConsulta.Juscraper, tribunal, null);
                return resultado;
            }

            motivo = JuscraperFalha.Motivos.NaoEncontrado;
        }
        else if (falha.NumeroInvalido || !config.FallbackDataJud)
        {
            telemetry.Registrar(FonteConsulta.Juscraper, tribunal, null);
            return resultado;
        }
        else
        {
            motivo = falha.Motivo;
        }

        var doDataJud = await dataJud.ConsultarAsync(cnjNumber, cancellationToken);
        telemetry.Registrar(FonteConsulta.DataJud, tribunal, motivo);

        if (doDataJud.IsSuccess)
        {
            logger.LogInformation("Consulta ao {Tribunal} atendida pelo DataJud (fallback: {Motivo}).", tribunal, motivo);

            // "Não encontrado" do portal continua valendo se o DataJud também não conhecer o processo.
            return doDataJud.Value is null && falha is null ? resultado : doDataJud;
        }

        logger.LogWarning(
            "Consulta ao {Tribunal} falhou nas duas fontes: juscraper {Motivo}, DataJud {CodigoDataJud}.",
            tribunal, motivo, doDataJud.Error.Code);

        // Fallback do "não encontrado" que falhou no DataJud: o "não existe" do portal é a melhor resposta.
        return falha is null ? resultado : doDataJud;
    }
}
