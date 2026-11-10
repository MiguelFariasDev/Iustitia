using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Atendimentos.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo Atendimento — um DbContext por módulo, mesmo banco físico (ADR-001).
/// <see cref="OutboxMessages"/> é mapeado aqui e excluído das migrations pelo mesmo motivo de
/// LegalDbContext: o OutboxInterceptor grava os eventos na MESMA transação do SaveChanges, mas
/// a tabela pertence ao Platform.
/// </summary>
public sealed class AtendimentoDbContext(DbContextOptions<AtendimentoDbContext> options, ITenantContext tenantContext)
    : BaseDbContext(options, tenantContext)
{
    public DbSet<Atendimento> Atendimentos => Set<Atendimento>();

    public DbSet<AtendimentoMensagem> Mensagens => Set<AtendimentoMensagem>();

    public DbSet<AtendimentoAuditoria> Auditoria => Set<AtendimentoAuditoria>();

    public DbSet<BotApiKey> BotApiKeys => Set<BotApiKey>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}
