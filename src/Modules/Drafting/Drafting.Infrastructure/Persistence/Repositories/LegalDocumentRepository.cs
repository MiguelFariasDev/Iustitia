using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;

public sealed class LegalDocumentRepository(DraftingDbContext dbContext)
    : Repository<LegalDocumentRequest, LegalDocumentRequestId>(dbContext), ILegalDocumentRepository
{
    public void AddChild(RetrievedSource source) => dbContext.RetrievedSources.Add(source);

    public void AddChild(LegalDocumentEvent evento) => dbContext.LegalDocumentEvents.Add(evento);

    public Task<LegalDocumentRequest?> GetWithSourcesAsync(
        LegalDocumentRequestId id, CancellationToken cancellationToken = default) =>
        dbContext.LegalDocumentRequests
            .Include(request => request.Sources)
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);

    public Task<PecaSnapshot?> GetSnapshotAsync(LegalDocumentRequestId id, CancellationToken cancellationToken = default) =>
        dbContext.LegalDocumentRequests
            .AsNoTracking()
            .Where(request => request.Id == id)
            .Select(request => new PecaSnapshot(
                request.Id.Value,
                request.UsuarioId,
                request.Status,
                request.EtapaAtual,
                request.Progresso,
                request.TextoGerado,
                request.Versao,
                request.Erro,
                request.CriadoEm,
                request.ConcluidoEm))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<LegalDocumentRequest>> ListRecentAsync(
        Guid? usuarioId, int skip, int take, CancellationToken cancellationToken = default) =>
        await dbContext.LegalDocumentRequests
            .AsNoTracking()
            .Where(request => usuarioId == null || request.UsuarioId == usuarioId)
            .OrderByDescending(request => request.CriadoEm)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
}
