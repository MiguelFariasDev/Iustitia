using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Atendimentos.Infrastructure.Persistence.Repositories;

public sealed class BotApiKeyRepository(AtendimentoDbContext dbContext) : IBotApiKeyRepository
{
    public void Add(BotApiKey key) => dbContext.BotApiKeys.Add(key);

    public Task<BotApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.BotApiKeys.FirstOrDefaultAsync(key => key.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BotApiKey>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.BotApiKeys.OrderByDescending(key => key.CreatedAt).ToListAsync(cancellationToken);
}
