using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Pecas;

namespace Advocacia.Modules.Drafting.Infrastructure.Persistence.Repositories;

public sealed class LegalDocumentRepository(DraftingDbContext dbContext)
    : Repository<LegalDocumentRequest, LegalDocumentRequestId>(dbContext), ILegalDocumentRepository
{
    public void AddChild(RetrievedSource source) => dbContext.RetrievedSources.Add(source);

    public void AddChild(LegalDocumentEvent evento) => dbContext.LegalDocumentEvents.Add(evento);
}
