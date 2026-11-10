using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Models;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Drafting.Infrastructure.Metricas;

/// <summary>
/// Painel do escritório: quantas peças, quantas falharam, tempo médio e custo ESTIMADO. A GPU é
/// compartilhada entre os escritórios, então tempo ligado/ocioso e custo real de infraestrutura não
/// pertencem a um escritório e ficam em gpu_instance_logs (para a operação), não aqui.
/// </summary>
public sealed class PecaMetricasService(
    DraftingDbContext db, IGpuStateService gpuState, IDateTimeProvider clock) : IPecaMetricasService
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

        return Result.Success(new PecaMetricasResponse(
            Total: porStatus.Sum(p => p.Quantidade),
            Concluidas: Quantas(LegalDocumentStatus.Concluido),
            Falhas: Quantas(LegalDocumentStatus.Falhou),
            Canceladas: Quantas(LegalDocumentStatus.Cancelado),
            EmAndamento: Quantas(
                LegalDocumentStatus.NaFila, LegalDocumentStatus.PreparandoGpu, LegalDocumentStatus.BuscandoContexto,
                LegalDocumentStatus.Gerando, LegalDocumentStatus.Salvando),
            NaFila: Quantas(LegalDocumentStatus.Pendente),
            TempoMedioGeracaoMs: tempoMedio,
            CustoEstimadoUsd: custo,
            MoedaCusto: "USD",
            ObservacaoCusto: "Estimativa só do tempo de geração × preço/hora configurado; não inclui subida e ociosidade da GPU, compartilhadas entre os escritórios.",
            Gpu: gpuState.Snapshot(clock.UtcNow).Estado));
    }
}
