using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Acervo;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;

public sealed class IndexedDocumentRepository(DraftingDbContext dbContext)
    : Repository<IndexedDocument, IndexedDocumentId>(dbContext), IIndexedDocumentRepository
{
    public Task<IndexedDocument?> FindByHashAsync(string hashConteudo, CancellationToken cancellationToken = default) =>
        dbContext.IndexedDocuments.FirstOrDefaultAsync(document => document.HashConteudo == hashConteudo, cancellationToken);

    public async Task<IReadOnlyList<IndexedDocument>> ListVisibleAsync(
        Guid usuarioId, int skip, int take, CancellationToken cancellationToken = default) =>
        await dbContext.IndexedDocuments
            .AsNoTracking()
            .Where(document => document.Visibilidade == DocumentVisibility.Escritorio || document.UsuarioId == usuarioId)
            .OrderByDescending(document => document.CriadoEm)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
}
