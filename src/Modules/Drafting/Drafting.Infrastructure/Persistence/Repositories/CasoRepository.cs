using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;

public sealed class CasoRepository(DraftingDbContext db) : ICasoRepository
{
    public void Add(Caso caso) => db.Casos.Add(caso);

    public void AddMembro(CasoMembro membro) => db.CasoMembros.Add(membro);

    public Task<Caso?> ObterAsync(CasoId id, CancellationToken cancellationToken = default) =>
        db.Casos.Include(c => c.Membros).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Caso>> ListarAsync(
        Guid? usuarioId, Guid? clienteId, int skip, int take, CancellationToken cancellationToken = default) =>
        await db.Casos
            .AsNoTracking()
            .Include(c => c.Membros)
            .Where(c => usuarioId == null || c.UsuarioIdResponsavel == usuarioId || c.Membros.Any(m => m.UsuarioId == usuarioId))
            .Where(c => clienteId == null || c.ClienteId == clienteId)
            .OrderByDescending(c => c.CriadoEm)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
}

public sealed class CasoDocumentoRepository(DraftingDbContext db) : ICasoDocumentoRepository
{
    public void Add(CasoDocumento documento) => db.CasoDocumentos.Add(documento);

    /// <summary>Os trechos saem junto (FK com cascade).</summary>
    public void Remover(CasoDocumento documento) => db.CasoDocumentos.Remove(documento);

    public Task<CasoDocumento?> ObterAsync(CasoDocumentoId id, CancellationToken cancellationToken = default) =>
        db.CasoDocumentos.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<CasoDocumento?> ObterPorHashAsync(CasoId casoId, string hash, CancellationToken cancellationToken = default) =>
        db.CasoDocumentos.FirstOrDefaultAsync(d => d.CasoId == casoId && d.HashSha256 == hash, cancellationToken);

    public async Task<IReadOnlyList<CasoDocumento>> ListarAsync(CasoId casoId, CancellationToken cancellationToken = default) =>
        await db.CasoDocumentos.AsNoTracking().Where(d => d.CasoId == casoId).OrderBy(d => d.CriadoEm).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CasoDocumentoChunk>> ListarChunksAsync(
        CasoDocumentoId id, CancellationToken cancellationToken = default) =>
        await db.CasoDocumentoChunks.AsNoTracking().Where(c => c.DocumentoId == id).OrderBy(c => c.Ordem).ToListAsync(cancellationToken);

    public async Task SubstituirChunksAsync(
        CasoDocumentoId id, IReadOnlyList<CasoDocumentoChunk> chunks, CancellationToken cancellationToken = default)
    {
        // Ids determinísticos: o EF não deixa remover e inserir a MESMA chave no mesmo SaveChanges.
        var antigos = await db.CasoDocumentoChunks.Where(c => c.DocumentoId == id).ToListAsync(cancellationToken);
        if (antigos.Count > 0)
        {
            db.CasoDocumentoChunks.RemoveRange(antigos);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Entries<CasoDocumentoChunk>().ToList().ForEach(e => e.State = EntityState.Detached);
        }

        db.CasoDocumentoChunks.AddRange(chunks);
    }
}

public sealed class EventoSegurancaRepository(DraftingDbContext db) : IEventoSegurancaRepository
{
    public void Add(EventoSegurancaIa evento) => db.EventosSeguranca.Add(evento);
}
