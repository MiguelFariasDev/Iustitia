using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Advocacia.Modules.Atendimentos.Domain.Consulta;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Atendimentos.Infrastructure.Persistence.Repositories;

public sealed class ConsultaSessaoRepository(AtendimentoDbContext dbContext) : IConsultaSessaoRepository
{
    public void Add(ConsultaSessao sessao) => dbContext.ConsultaSessoes.Add(sessao);

    public Task<ConsultaSessao?> GetLatestByAtendimentoAsync(
        AtendimentoId atendimentoId, CancellationToken cancellationToken = default) =>
        dbContext.ConsultaSessoes
            .Where(sessao => sessao.AtendimentoId == atendimentoId)
            .OrderByDescending(sessao => sessao.StartedAt)
            // Desempate DETERMINÍSTICO: recomeçar invalida a anterior (ExpiresAt = agora) e cria a nova
            // (ExpiresAt = agora + TTL). Desempatar por Id (Guid aleatório) poderia escolher a velha.
            .ThenByDescending(sessao => sessao.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<DateTimeOffset?> GetBlockedUntilAsync(
        Guid tenantId, Guid clientId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await dbContext.ConsultaSessoes
            .Where(sessao => sessao.TenantId == tenantId && sessao.ClientId == clientId && sessao.BlockedUntil > now)
            .MaxAsync(sessao => sessao.BlockedUntil, cancellationToken);

    public async Task<int> GetRecentFailuresAsync(
        Guid tenantId, Guid clientId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        await dbContext.ConsultaSessoes
            .Where(sessao => sessao.TenantId == tenantId
                && sessao.ClientId == clientId
                && sessao.StartedAt >= since
                && sessao.VerifiedAt == null
                && sessao.BlockedUntil == null)
            .MaxAsync(sessao => (int?)sessao.FailedAttempts, cancellationToken) ?? 0;
}
