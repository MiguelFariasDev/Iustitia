using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;
using Advocacia.Modules.Drafting.Domain.Acervo;
using Advocacia.Modules.Drafting.Domain.Casos;
using Advocacia.Modules.Drafting.Domain.Fila;
using Advocacia.Modules.Drafting.Domain.Gpu;
using Advocacia.Modules.Drafting.Domain.Normas;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Advocacia.Modules.Drafting.Domain.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence;

/// <summary>DbContext do módulo Drafting — um por módulo, mesmo banco físico (ADR-001).</summary>
public sealed class DraftingDbContext(DbContextOptions<DraftingDbContext> options, ITenantContext tenantContext)
    : BaseDbContext(options, tenantContext)
{
    public DbSet<LegalDocumentRequest> LegalDocumentRequests => Set<LegalDocumentRequest>();

    public DbSet<RetrievedSource> RetrievedSources => Set<RetrievedSource>();

    public DbSet<LegalDocumentEvent> LegalDocumentEvents => Set<LegalDocumentEvent>();

    public DbSet<GenerationQueueItem> GenerationQueue => Set<GenerationQueueItem>();

    public DbSet<GpuInstanceLog> GpuInstanceLogs => Set<GpuInstanceLog>();

    /// <summary>Legado (acervo do escritório, sem isolamento por caso). Nada lê nem escreve; mantido para não apagar dados.</summary>
    public DbSet<IndexedDocument> IndexedDocuments => Set<IndexedDocument>();

    public DbSet<CitacaoPeca> Citacoes => Set<CitacaoPeca>();

    public DbSet<Caso> Casos => Set<Caso>();

    public DbSet<CasoMembro> CasoMembros => Set<CasoMembro>();

    public DbSet<CasoDocumento> CasoDocumentos => Set<CasoDocumento>();

    public DbSet<CasoDocumentoChunk> CasoDocumentoChunks => Set<CasoDocumentoChunk>();

    public DbSet<EventoSegurancaIa> EventosSeguranca => Set<EventoSegurancaIa>();

    public DbSet<NormaJuridica> Normas => Set<NormaJuridica>();

    public DbSet<NormaChunk> NormaChunks => Set<NormaChunk>();

    public DbSet<NormaVersao> NormaVersoes => Set<NormaVersao>();

    public DbSet<IngestaoJob> IngestaoJobs => Set<IngestaoJob>();
}
