using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Fila;
using Advocacia.Modules.Drafting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Drafting.Infrastructure.Fila;

/// <summary>
/// Fila persistente no banco relacional (sem Redis). A exclusão mútua é feita por COMPARE-AND-SET:
/// cada reserva é um <c>UPDATE … WHERE id = X AND (ainda elegível)</c> e só quem afeta 1 linha leva o
/// item — dois workers (ou duas réplicas) nunca processam o mesmo, sem transação longa nem lock
/// explícito. É portátil entre bancos relacionais e dispensa <c>FOR UPDATE SKIP LOCKED</c>.
///
/// Reserva vencida (o worker caiu) volta a ser elegível sozinha: o item não fica preso.
/// </summary>
public sealed class GeracaoPecaFila(DraftingDbContext db, IDateTimeProvider clock) : IGeracaoPecaFila
{
    public void Enfileirar(Guid tenantId, QueueItemKind tipo, Guid referenciaId) =>
        db.GenerationQueue.Add(new GenerationQueueItem(tenantId, tipo, referenciaId, clock.UtcNow));

    public async Task<IReadOnlyList<ReservedQueueItem>> ReservarAsync(
        string worker, int max, TimeSpan reserva, CancellationToken cancellationToken = default)
    {
        var agora = clock.UtcNow;
        var reservados = new List<ReservedQueueItem>();

        // Várias passadas: com muita concorrência, os candidatos de uma leitura podem ter sido levados
        // por outro worker; reler traz os que sobraram, em vez de voltar com o lote incompleto.
        for (var passada = 0; passada < 5 && reservados.Count < max; passada++)
        {
            var candidatos = await db.GenerationQueue
                .AsNoTracking()
                .Where(i => (i.Status == QueueItemStatus.Pendente && i.DisponivelEm <= agora)
                            || (i.Status == QueueItemStatus.Reservado && i.ReservadoAte < agora))
                .OrderBy(i => i.CriadoEm)
                .Select(i => i.Id)
                .Take(max * 3)
                .ToListAsync(cancellationToken);

            if (candidatos.Count == 0)
            {
                break;
            }

            foreach (var id in candidatos)
            {
                if (reservados.Count >= max)
                {
                    break;
                }

                var linhas = await db.GenerationQueue
                    .Where(i => i.Id == id
                                && ((i.Status == QueueItemStatus.Pendente && i.DisponivelEm <= agora)
                                    || (i.Status == QueueItemStatus.Reservado && i.ReservadoAte < agora)))
                    .ExecuteUpdateAsync(
                        set => set
                            .SetProperty(i => i.Status, QueueItemStatus.Reservado)
                            .SetProperty(i => i.ReservadoPor, worker)
                            .SetProperty(i => i.ReservadoEm, agora)
                            .SetProperty(i => i.ReservadoAte, agora + reserva)
                            .SetProperty(i => i.Tentativas, i => i.Tentativas + 1),
                        cancellationToken);

                if (linhas == 1)
                {
                    reservados.Add(await LoadAsync(id, cancellationToken));
                }
            }
        }

        return reservados;
    }

    public async Task<IReadOnlyList<ReservedQueueItem>> ReservarExpiradosAsync(
        string worker, DateTimeOffset criadosAntes, int max, TimeSpan reserva, CancellationToken cancellationToken = default)
    {
        var agora = clock.UtcNow;
        var candidatos = await db.GenerationQueue
            .AsNoTracking()
            .Where(i => i.Status == QueueItemStatus.Pendente && i.CriadoEm < criadosAntes)
            .OrderBy(i => i.CriadoEm)
            .Select(i => i.Id)
            .Take(max)
            .ToListAsync(cancellationToken);

        var reservados = new List<ReservedQueueItem>();
        foreach (var id in candidatos)
        {
            var linhas = await db.GenerationQueue
                .Where(i => i.Id == id && i.Status == QueueItemStatus.Pendente && i.CriadoEm < criadosAntes)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(i => i.Status, QueueItemStatus.Reservado)
                        .SetProperty(i => i.ReservadoPor, worker)
                        .SetProperty(i => i.ReservadoEm, agora)
                        .SetProperty(i => i.ReservadoAte, agora + reserva),
                    cancellationToken);

            if (linhas == 1)
            {
                reservados.Add(await LoadAsync(id, cancellationToken));
            }
        }

        return reservados;
    }

    public async Task FinalizarAsync(
        Guid itemId, QueueItemStatus status, string? erro, CancellationToken cancellationToken = default)
    {
        var agora = clock.UtcNow;
        var texto = erro is { Length: > GenerationQueueItem.ErroMaxLength } ? erro[..GenerationQueueItem.ErroMaxLength] : erro;

        await db.GenerationQueue
            .Where(i => i.Id == itemId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(i => i.Status, status)
                    .SetProperty(i => i.ConcluidoEm, agora)
                    .SetProperty(i => i.UltimoErro, texto)
                    .SetProperty(i => i.ReservadoAte, (DateTimeOffset?)null),
                cancellationToken);
    }

    public async Task CancelarPorReferenciaAsync(Guid referenciaId, CancellationToken cancellationToken = default)
    {
        var agora = clock.UtcNow;

        // Só o que ainda não foi pego por um worker. Se já está sendo processado, quem encerra é o
        // próprio processador, ao ver a solicitação cancelada.
        await db.GenerationQueue
            .Where(i => i.ReferenciaId == referenciaId && i.Status == QueueItemStatus.Pendente)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(i => i.Status, QueueItemStatus.Cancelado)
                    .SetProperty(i => i.ConcluidoEm, agora),
                cancellationToken);
    }

    public async Task ReagendarAsync(
        Guid itemId, DateTimeOffset disponivelEm, string erro, bool consumirTentativa, CancellationToken cancellationToken = default)
    {
        var texto = erro.Length > GenerationQueueItem.ErroMaxLength ? erro[..GenerationQueueItem.ErroMaxLength] : erro;
        var query = db.GenerationQueue.Where(i => i.Id == itemId);

        if (consumirTentativa)
        {
            await query.ExecuteUpdateAsync(
                set => set
                    .SetProperty(i => i.Status, QueueItemStatus.Pendente)
                    .SetProperty(i => i.DisponivelEm, disponivelEm)
                    .SetProperty(i => i.UltimoErro, texto)
                    .SetProperty(i => i.ReservadoPor, (string?)null)
                    .SetProperty(i => i.ReservadoAte, (DateTimeOffset?)null),
                cancellationToken);

            return;
        }

        // Falha da infraestrutura, não da geração: devolve a tentativa que a reserva consumiu.
        await query.ExecuteUpdateAsync(
            set => set
                .SetProperty(i => i.Status, QueueItemStatus.Pendente)
                .SetProperty(i => i.DisponivelEm, disponivelEm)
                .SetProperty(i => i.UltimoErro, texto)
                .SetProperty(i => i.ReservadoPor, (string?)null)
                .SetProperty(i => i.ReservadoAte, (DateTimeOffset?)null)
                .SetProperty(i => i.Tentativas, i => i.Tentativas > 0 ? i.Tentativas - 1 : 0),
            cancellationToken);
    }

    public Task<int> ContarDisponiveisAsync(CancellationToken cancellationToken = default)
    {
        var agora = clock.UtcNow;

        return db.GenerationQueue.CountAsync(
            i => (i.Status == QueueItemStatus.Pendente && i.DisponivelEm <= agora)
                 || (i.Status == QueueItemStatus.Reservado && i.ReservadoAte < agora),
            cancellationToken);
    }

    public Task<int> ContarTrabalhoAbertoAsync(CancellationToken cancellationToken = default) =>
        db.GenerationQueue.CountAsync(
            i => i.Status == QueueItemStatus.Pendente || i.Status == QueueItemStatus.Reservado, cancellationToken);

    public async Task<DateTimeOffset?> UltimaAtividadeAsync(CancellationToken cancellationToken = default)
    {
        var criado = await db.GenerationQueue.MaxAsync(i => (DateTimeOffset?)i.CriadoEm, cancellationToken);
        var reservado = await db.GenerationQueue.MaxAsync(i => i.ReservadoEm, cancellationToken);
        var concluido = await db.GenerationQueue.MaxAsync(i => i.ConcluidoEm, cancellationToken);

        return new[] { criado, reservado, concluido }.Max();
    }

    private async Task<ReservedQueueItem> LoadAsync(Guid id, CancellationToken ct) =>
        await db.GenerationQueue
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new ReservedQueueItem(i.Id, i.TenantId, i.Tipo, i.ReferenciaId, i.Tentativas, i.CriadoEm))
            .FirstAsync(ct);
}
