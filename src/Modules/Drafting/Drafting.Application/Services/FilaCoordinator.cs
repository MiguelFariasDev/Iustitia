using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Fila;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Um ciclo do worker: reserva trabalho em lote, liga a GPU UMA vez para o lote, processa e, sem
/// mais trabalho, deixa a política de ociosidade desligá-la. O worker (BackgroundService) só chama
/// <see cref="RunCycleAsync"/> de tempos em tempos; toda a lógica testável mora aqui.
///
/// Cada item roda no SEU escopo de DI, com o tenant da solicitação definido (RLS) e dentro de uma
/// Task separada: o tenant fica num AsyncLocal, e sem a Task ele "vazaria" para o resto do ciclo e
/// passaria a filtrar a reserva dos itens seguintes por um escritório só.
/// </summary>
public sealed class FilaCoordinator(
    IServiceScopeFactory scopes,
    IOptions<FilaOptions> filaOptions,
    IDateTimeProvider clock,
    ILogger<FilaCoordinator> logger)
{
    private readonly string _worker = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    /// <summary>Verdadeiro se havia trabalho (o chamador volta logo, sem esperar o intervalo).</summary>
    public async Task<bool> RunCycleAsync(CancellationToken cancellationToken)
    {
        var options = filaOptions.Value;
        var reserva = TimeSpan.FromMinutes(options.ReservaMinutos);
        var worked = false;

        await ExpireAsync(reserva, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<ReservedQueueItem> batch;
            using (var scope = scopes.CreateScope())
            {
                batch = await scope.ServiceProvider.GetRequiredService<IGeracaoPecaFila>()
                    .ReservarAsync(_worker, Math.Max(1, options.LoteMax), reserva, cancellationToken);
            }

            if (batch.Count == 0)
            {
                break;
            }

            worked = true;
            logger.LogInformation("Fila: {Itens} item(ns) reservado(s) para processar.", batch.Count);

            foreach (var item in batch)
            {
                await InItemScopeAsync(item, (handler, ct) => handler.AoReservarAsync(item.ReferenciaId, ct), cancellationToken);
            }

            var gpu = await EnsureGpuAsync(batch, cancellationToken);
            if (gpu.IsFailure)
            {
                await ReleaseBatchAsync(batch, cancellationToken);

                break; // não insiste neste ciclo: o circuito e o intervalo do worker cuidam da nova tentativa
            }

            await ProcessBatchAsync(batch, options.Paralelismo, cancellationToken);
        }

        if (!worked && !cancellationToken.IsCancellationRequested)
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IGpuOrchestrator>().PararSeOciosaAsync(cancellationToken);
        }

        return worked;
    }

    private async Task<Result> EnsureGpuAsync(
        IReadOnlyList<ReservedQueueItem> batch, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IGpuOrchestrator>();

        return await orchestrator.GarantirProntaAsync(
            $"{batch.Count} item(ns) na fila",
            async token =>
            {
                // Só avisa "preparando IA" se a GPU realmente precisa subir.
                foreach (var item in batch)
                {
                    await InItemScopeAsync(item, (handler, c) => handler.AoPrepararGpuAsync(item.ReferenciaId, c), token);
                }
            },
            ct);
    }

    private async Task ReleaseBatchAsync(IReadOnlyList<ReservedQueueItem> batch, CancellationToken ct)
    {
        var retryAt = clock.UtcNow.AddSeconds(Math.Max(1, filaOptions.Value.IntervaloSegundos) * 2);
        foreach (var item in batch)
        {
            await InItemScopeAsync(
                item,
                async (handler, c) =>
                {
                    await handler.AoFaltarInfraAsync(item.ReferenciaId, "GPU indisponível", c);
                    using var scope = scopes.CreateScope();
                    // Sem consumir tentativa: a falha foi da infraestrutura, não da geração.
                    await scope.ServiceProvider.GetRequiredService<IGeracaoPecaFila>()
                        .ReagendarAsync(item.Id, retryAt, "GPU indisponível", consumirTentativa: false, c);
                },
                ct);
        }
    }

    private async Task ProcessBatchAsync(IReadOnlyList<ReservedQueueItem> batch, int paralelismo, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(Math.Max(1, paralelismo));
        var tasks = batch.Select(async item =>
        {
            await gate.WaitAsync(ct);
            try
            {
                await ProcessItemAsync(item, ct);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private Task ProcessItemAsync(ReservedQueueItem item, CancellationToken ct) =>
        Task.Run(
            async () =>
            {
                using var scope = scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(item.TenantId);
                var handler = ResolveHandler(scope, item.Tipo);
                var fila = scope.ServiceProvider.GetRequiredService<IGeracaoPecaFila>();

                WorkOutcome outcome;
                try
                {
                    outcome = await handler.ProcessarAsync(item.ReferenciaId, item.Tentativas, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    outcome = new WorkOutcome(WorkOutcomeKind.Interrompido, "Processo interrompido.");
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Fila: erro inesperado ao processar {Tipo} {Referencia}.", item.Tipo, item.ReferenciaId);
                    outcome = new WorkOutcome(WorkOutcomeKind.Falhou, $"{exception.GetType().Name}: {exception.Message}");
                    await TryAsync(() => handler.AoFalharDefinitivoAsync(item.ReferenciaId, outcome.Erro ?? "erro", CancellationToken.None));
                }

                await ApplyOutcomeAsync(fila, item, outcome);
            },
            CancellationToken.None);

    private async Task ApplyOutcomeAsync(IGeracaoPecaFila fila, ReservedQueueItem item, WorkOutcome outcome)
    {
        // O resultado do item precisa ser gravado mesmo com o serviço desligando.
        var token = CancellationToken.None;
        switch (outcome.Kind)
        {
            case WorkOutcomeKind.Concluido:
                await fila.FinalizarAsync(item.Id, QueueItemStatus.Concluido, null, token);
                break;
            case WorkOutcomeKind.Cancelado:
                await fila.FinalizarAsync(item.Id, QueueItemStatus.Cancelado, null, token);
                break;
            case WorkOutcomeKind.Falhou:
                await fila.FinalizarAsync(item.Id, QueueItemStatus.Falhou, outcome.Erro, token);
                break;
            case WorkOutcomeKind.Interrompido:
                await fila.ReagendarAsync(item.Id, clock.UtcNow, outcome.Erro ?? "interrompido", consumirTentativa: false, token);
                break;
            default:
                await fila.ReagendarAsync(
                    item.Id, clock.UtcNow + filaOptions.Value.Backoff(item.Tentativas), outcome.Erro ?? "falha transitória",
                    consumirTentativa: true, token);
                break;
        }
    }

    private async Task ExpireAsync(TimeSpan reserva, CancellationToken ct)
    {
        var limite = clock.UtcNow.AddMinutes(-filaOptions.Value.ExpiraAposMinutos);

        IReadOnlyList<ReservedQueueItem> expired;
        using (var scope = scopes.CreateScope())
        {
            expired = await scope.ServiceProvider.GetRequiredService<IGeracaoPecaFila>()
                .ReservarExpiradosAsync(_worker, limite, 50, reserva, ct);
        }

        foreach (var item in expired)
        {
            logger.LogWarning("Fila: item {Item} expirou sem ser processado.", item.Id);
            await InItemScopeAsync(
                item,
                async (handler, c) =>
                {
                    await handler.AoExpirarAsync(item.ReferenciaId, c);
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IGeracaoPecaFila>()
                        .FinalizarAsync(item.Id, QueueItemStatus.Falhou, "Tempo máximo de espera esgotado.", c);
                },
                ct);
        }
    }

    /// <summary>Executa uma ação do handler no escopo (e no tenant) do item, isolada numa Task própria.</summary>
    private Task InItemScopeAsync(
        ReservedQueueItem item, Func<IQueueWorkHandler, CancellationToken, Task> action, CancellationToken ct) =>
        Task.Run(
            async () =>
            {
                using var scope = scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(item.TenantId);
                await action(ResolveHandler(scope, item.Tipo), ct);
            },
            CancellationToken.None);

    private static IQueueWorkHandler ResolveHandler(IServiceScope scope, QueueItemKind tipo) =>
        scope.ServiceProvider.GetServices<IQueueWorkHandler>().First(handler => handler.Tipo == tipo);

    private async Task TryAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Fila: não foi possível registrar a falha do item.");
        }
    }
}
