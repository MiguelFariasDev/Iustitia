using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Drafting.Infrastructure.Metricas;

/// <summary>
/// Painel do escritório: quantas peças, quantas falharam, tempo médio e custo ESTIMADO. A GPU é
/// compartilhada entre os escritórios, então tempo ligado/ocioso e custo real de infraestrutura não
/// pertencem a um escritório e ficam em gpu_instance_logs (para a operação), não aqui.
/// </summary>
public sealed class PecaMetricasService(
    DraftingDbContext db, IGpuStateService gpuState, IOptions<CustoOptions> custoOptions, IDateTimeProvider clock) : IPecaMetricasService
{
    public async Task<Result<PecaMetricasResponse>> ObterAsync(CancellationToken cancellationToken = default)
    {
        var porStatus = await db.LegalDocumentRequests
            .AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        int Quantas(params LegalDocumentStatus[] statuses) =>
            porStatus.Where(p => statuses.Contains(p.Status)).Sum(p => p.Quantidade);

        var tempoMedio = await db.LegalDocumentRequests
            .AsNoTracking()
            .Where(r => r.Status == LegalDocumentStatus.Concluido && r.TempoGeracaoMs != null)
            .AverageAsync(r => (double?)r.TempoGeracaoMs, cancellationToken);

        var custo = await db.LegalDocumentRequests
            .AsNoTracking()
            .SumAsync(r => r.CustoEstimadoUsd ?? 0m, cancellationToken);

        var tempoTotal = await db.LegalDocumentRequests.AsNoTracking().SumAsync(r => r.TempoGeracaoMs ?? 0, cancellationToken);
        var aguardandoRevisao = await db.LegalDocumentRequests.AsNoTracking()
            .CountAsync(r => r.Status == LegalDocumentStatus.Concluido && r.RevisadoEm == null, cancellationToken);
        var anexosAbertos = await db.CasoDocumentos.AsNoTracking()
            .CountAsync(d => d.Status != CasoDocumentoStatus.Indexado && d.Status != CasoDocumentoStatus.Falhou, cancellationToken);
        var inicio = clock.UtcNow.AddDays(-30);
        var eventos = await db.EventosSeguranca.AsNoTracking().CountAsync(e => e.OcorridoEm >= inicio, cancellationToken);

        // Transições da GPU (infraestrutura compartilhada): só horas e custo agregados, sem dado de escritório.
        var transicoes = await db.GpuInstanceLogs.AsNoTracking()
            .Where(l => l.Sucesso && l.CriadoEm >= inicio && (l.Acao == "Iniciar" || l.Acao == "Parar"))
            .OrderBy(l => l.CriadoEm)
            .Select(l => new { l.Acao, l.CriadoEm })
            .ToListAsync(cancellationToken);
        var horas = HorasLigada(transicoes.Select(t => (t.Acao == "Iniciar", t.CriadoEm)), clock.UtcNow);

        return Result.Success(new PecaMetricasResponse(
            Total: porStatus.Sum(p => p.Quantidade),
            Concluidas: Quantas(LegalDocumentStatus.Concluido),
            Falhas: Quantas(LegalDocumentStatus.Falhou),
            Canceladas: Quantas(LegalDocumentStatus.Cancelado),
            EmAndamento: Quantas(
                LegalDocumentStatus.NaFila, LegalDocumentStatus.PreparandoGpu, LegalDocumentStatus.BuscandoContexto,
                LegalDocumentStatus.BuscandoDocumentosCaso, LegalDocumentStatus.BuscandoJuridico, LegalDocumentStatus.Gerando,
                LegalDocumentStatus.Validando, LegalDocumentStatus.Salvando),
            NaFila: Quantas(LegalDocumentStatus.Pendente),
            TempoMedioGeracaoMs: tempoMedio,
            CustoEstimadoUsd: custo,
            MoedaCusto: "USD",
            ObservacaoCusto: "Estimativa só do tempo de geração × preço/hora configurado; não inclui subida e ociosidade da GPU, compartilhadas entre os escritórios.",
            Gpu: gpuState.Snapshot(clock.UtcNow).Estado,
            TempoGeracaoTotalMs: tempoTotal,
            GpuHorasUltimos30Dias: Math.Round(horas, 2),
            GpuCustoEstimadoUltimos30DiasUsd: Math.Round((decimal)horas * custoOptions.Value.GpuUsdPorHora, 2),
            AguardandoRevisao: aguardandoRevisao,
            AnexosEmProcessamento: anexosAbertos,
            EventosSeguranca30Dias: eventos));
    }

    /// <summary>Soma os intervalos Iniciar→Parar; "Iniciar" sem "Parar" conta até agora.</summary>
    internal static double HorasLigada(IEnumerable<(bool Ligou, DateTimeOffset Em)> transicoes, DateTimeOffset agora)
    {
        var total = TimeSpan.Zero;
        DateTimeOffset? ligadaDesde = null;
        foreach (var (ligou, em) in transicoes)
        {
            if (ligou)
            {
                ligadaDesde ??= em;
            }
            else if (ligadaDesde is { } desde)
            {
                total += em - desde;
                ligadaDesde = null;
            }
        }

        if (ligadaDesde is { } aberta)
        {
            total += agora - aberta;
        }

        return total.TotalHours;
    }
}
