using Advocacia.Modules.Atendimentos.Domain.Atendimentos;

namespace Advocacia.Modules.Atendimentos.Application.Abstractions;

public interface IBotApiKeyRepository
{
    void Add(BotApiKey key);

    Task<BotApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BotApiKey>> ListAsync(CancellationToken cancellationToken = default);
}
