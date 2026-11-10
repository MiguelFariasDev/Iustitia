using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Atendimentos.Application;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Atendimentos.Infrastructure.Bot;

public sealed class BotCredentialValidator(AtendimentoDbContext dbContext, IDateTimeProvider clock) : IBotCredentialValidator
{
    // Gravar LastUsedAt a cada requisição do Bot transformaria toda leitura em escrita.
    private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(5);

    public async Task<BotCredential?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith(BotApiKeyGenerator.KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var hash = BotApiKeyGenerator.Hash(apiKey);

        // IgnoreQueryFilters: aqui o tenant ainda é DESCONHECIDO — é a chave que o revela. A
        // tabela bot_api_keys não tem RLS pelo mesmo motivo (ver 014_atendimentos.sql).
        var key = await dbContext.BotApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.KeyHash == hash && candidate.RevokedAt == null, cancellationToken);

        if (key is null)
        {
            return null;
        }

        var now = clock.UtcNow;
        if (key.LastUsedAt is null || now - key.LastUsedAt > LastUsedResolution)
        {
            key.MarkUsed(now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new BotCredential(key.Id, key.TenantId);
    }
}
