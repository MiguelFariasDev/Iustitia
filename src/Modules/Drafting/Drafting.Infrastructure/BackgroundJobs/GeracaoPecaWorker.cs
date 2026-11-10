using Advocacia.Modules.Drafting.Application.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.BackgroundJobs;

/// <summary>
/// Laço contínuo que consome a fila de peças e liga/desliga a GPU. Roda o tempo todo, mas NÃO exige
/// GPU ligada: a cada <c>Drafting:Fila:IntervaloSegundos</c> olha o banco, e só quando há trabalho
/// o <see cref="FilaCoordinator"/> sobe a instância. Com a fila vazia, a política de ociosidade a desliga.
/// Registre-o só no host de jobs (Worker); a API enfileira e consulta, não processa.
/// </summary>
public sealed class GeracaoPecaWorker(
    FilaCoordinator coordinator,
    IOptions<DraftingOptions> draftingOptions,
    IOptions<FilaOptions> filaOptions,
    ILogger<GeracaoPecaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!draftingOptions.Value.Enabled)
        {
            logger.LogInformation("Peças: geração desabilitada (Drafting:Enabled=false); o worker de fila não será executado.");

            return;
        }

        logger.LogInformation("Peças: worker de fila iniciado (intervalo {Intervalo}s).", filaOptions.Value.IntervaloSegundos);

        while (!stoppingToken.IsCancellationRequested)
        {
            var worked = false;
            try
            {
                worked = await coordinator.RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Um ciclo ruim (banco fora do ar, bug) não derruba o worker: tenta de novo no próximo intervalo.
                logger.LogError(exception, "Peças: falha no ciclo do worker de fila.");
            }

            try
            {
                // Se havia trabalho, olha logo de novo (pode ter chegado mais enquanto processava).
                var espera = worked ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(Math.Max(1, filaOptions.Value.IntervaloSegundos));
                await Task.Delay(espera, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Peças: worker de fila encerrado.");
    }
}
