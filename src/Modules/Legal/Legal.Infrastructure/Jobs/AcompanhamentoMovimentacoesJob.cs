using System.Diagnostics;
using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Application.Features.Movimentacoes;
using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Legal.Infrastructure.Jobs;

/// <summary>
/// Acompanhamento noturno de movimentações (ADR-075): para cada escritório, sincroniza um a um os
/// processos ativos e suspensos (<see cref="SincronizarMovimentacoesCommand"/>).
///
/// <list type="bullet">
/// <item>Cada processo roda num ESCOPO de DI próprio: uma falha de gravação (ex.: corrida com a
/// sincronização manual no índice único) não contamina o DbContext dos processos seguintes.</item>
/// <item>Falha de um processo não relança: conta e segue. Tribunal sem cobertura é contado à parte.
/// Sem retry automático do Hangfire — a próxima noite é a nova tentativa, com janela sobreposta.</item>
/// <item>O job NUNCA envia nada ao cliente: só alerta o advogado e deixa rascunhos para revisão.</item>
/// </list>
///
/// Log só com contadores e ids — nunca andamento, nome de parte ou e-mail (claude.md, seção 14).
/// </summary>
public sealed class AcompanhamentoMovimentacoesJob(
    IProcessoRepository processoRepository,
    IServiceScopeFactory scopeFactory,
    ITenantContext tenantContext,
    IOptions<AcompanhamentoMovimentacoesJobOptions> options,
    ILogger<AcompanhamentoMovimentacoesJob> logger)
{
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 6 * 60 * 60)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!config.Enabled)
        {
            logger.LogInformation("AcompanhamentoMovimentacoesJob: desabilitado (Jobs:AcompanhamentoMovimentacoes:Enabled=false).");
            return;
        }

        var tenantIds = config.TenantIds.Count > 0
            ? config.TenantIds
            : await processoRepository.ListTenantsComProcessosAtivosAsync(cancellationToken);

        var totais = new Totais();
        var cronometro = Stopwatch.StartNew();

        foreach (var tenantId in tenantIds)
        {
            // Fora de requisição HTTP: sem o tenant, o filtro global e a RLS devolvem vazio (fail closed).
            tenantContext.SetTenant(tenantId);
            var processos = await processoRepository.ListParaCapturaAsync(tenantId, cancellationToken);

            foreach (var processo in processos)
            {
                if (totais.Processados > 0 && config.PausaEntreProcessosMs > 0)
                {
                    await Task.Delay(config.PausaEntreProcessosMs, cancellationToken);
                }

                totais.Processados++;
                await SincronizarAsync(tenantId, processo.Id.Value, config.JanelaDias, totais, cancellationToken);
            }
        }

        cronometro.Stop();
        logger.LogInformation(
            "AcompanhamentoMovimentacoesJob concluído em {ElapsedMs}ms: {Escritorios} escritórios, {Processados} processos, " +
            "{Bases} bases, {ComNovidade} com novidade ({Novos} andamentos), {SemCobertura} sem cobertura, {Falhas} falhas.",
            cronometro.ElapsedMilliseconds, tenantIds.Count, totais.Processados, totais.Bases, totais.ComNovidade,
            totais.Novos, totais.SemCobertura, totais.Falhas);
    }

    private async Task SincronizarAsync(Guid tenantId, Guid processoId, int janelaDias, Totais totais, CancellationToken cancellationToken)
    {
        await using var escopo = scopeFactory.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var sender = escopo.ServiceProvider.GetRequiredService<ISender>();

        try
        {
            var resultado = await sender.Send(
                new SincronizarMovimentacoesCommand(processoId, OrigemJob: true, JanelaDias: janelaDias), cancellationToken);

            if (resultado.IsFailure)
            {
                if (resultado.Error.Code == nameof(ErrorCode.CNJ_TRIBUNAL_NOT_SUPPORTED))
                {
                    totais.SemCobertura++;
                }
                else
                {
                    totais.Falhas++;
                    logger.LogWarning("Acompanhamento: processo {ProcessoId} não sincronizado ({Codigo}).", processoId, resultado.Error.Code);
                }

                return;
            }

            var sincronizacao = resultado.Value;
            if (sincronizacao.Base)
            {
                totais.Bases++;
            }
            else if (sincronizacao.Novos > 0)
            {
                totais.ComNovidade++;
                totais.Novos += sincronizacao.Novos;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Só o tipo: a mensagem de uma exceção de banco pode ecoar valores (texto do andamento).
            totais.Falhas++;
            logger.LogError("Acompanhamento: falha ao sincronizar o processo {ProcessoId} ({Tipo}).", processoId, exception.GetType().Name);
        }
    }

    private sealed class Totais
    {
        public int Processados { get; set; }

        public int Bases { get; set; }

        public int ComNovidade { get; set; }

        public int Novos { get; set; }

        public int SemCobertura { get; set; }

        public int Falhas { get; set; }
    }
}
