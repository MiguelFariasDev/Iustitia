using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Normas;
using Advocacia.Modules.Drafting.Domain.Normas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.BackgroundJobs;

/// <summary>
/// Mantém o acervo normativo atualizado, só no host Worker. Roda um ciclo quando (a) há pedido manual
/// pendente (POST /admin/normas/sincronizar), (b) nunca rodou ou (c) passou <c>IntervaloHoras</c> desde o
/// último. Ciclo parcial (mais arquivos que <c>LimiteArquivosPorCiclo</c>) emenda no seguinte sem esperar:
/// é assim que a primeira carga avança em fatias, por prioridade, sem travar nada.
/// </summary>
public sealed class NormaIngestaoWorker(
    IServiceScopeFactory scopeFactory,
    INormaSyncOrchestrator orchestrator,
    IOptions<NormasIngestaoOptions> options,
    IDateTimeProvider clock,
    ILogger<NormaIngestaoWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerHabilitado)
        {
            logger.LogInformation("Ingestão do acervo normativo desabilitada (Ingestao:WorkerHabilitado=false).");

            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        var continuar = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                continuar = await TickAsync(continuar, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Um ciclo que quebra não derruba o worker (a fila de geração roda no mesmo processo).
                logger.LogError(ex, "Ciclo de ingestão do acervo falhou.");
                continuar = false;
            }

            if (!continuar)
            {
                await Task.Delay(Poll, stoppingToken);
            }
        }
    }

    private async Task<bool> TickAsync(bool emendar, CancellationToken cancellationToken)
    {
        Guid? jobId = null;
        var tipo = IngestaoJobTipo.Incremental;

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<INormaStore>();
            var pendente = await store.ObterJobPendenteAsync(cancellationToken);
            if (pendente is not null)
            {
                jobId = pendente.Id;
                tipo = pendente.Tipo;
            }
            else if (!emendar)
            {
                var ultima = await store.UltimaSincronizacaoAsync(cancellationToken);
                var devida = ultima is null || clock.UtcNow - ultima >= TimeSpan.FromHours(Math.Max(1, options.Value.IntervaloHoras));
                if (!devida)
                {
                    return false;
                }

                tipo = ultima is null ? IngestaoJobTipo.Completa : IngestaoJobTipo.Incremental;
            }
        }

        var resumo = await orchestrator.SincronizarAsync(tipo, jobId, cancellationToken);
        logger.LogInformation(
            "Ingestão concluída: {Novos} novos, {Atualizados} atualizados, {Sem} sem mudança, {Ignorados} ignorados, {Removidos} removidos, {Erros} erros{Parcial}.",
            resumo.Novos, resumo.Atualizados, resumo.SemMudanca, resumo.Ignorados, resumo.Removidos, resumo.Erros,
            resumo.Parcial ? " (parcial: continua no próximo ciclo)" : string.Empty);

        // Só emenda quando houve progresso: parcial sem nenhum processado = travado, espera o próximo intervalo.
        return resumo.Parcial && resumo.Erros == 0 && (resumo.Novos + resumo.Atualizados) > 0;
    }
}
