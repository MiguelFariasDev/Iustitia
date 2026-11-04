using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.DbContext;
using Advocacia.BuildingBlocks.Infrastructure.Persistence.Outbox;
using Advocacia.Modules.Legal.Domain.Clientes;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Suggestions;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Legal.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo Legal — um DbContext por módulo, mesmo banco físico (ver ADR-001).
/// Os filtros globais de soft delete e isolamento por tenant vêm de
/// <see cref="BaseDbContext"/>; as configurações Fluent API são descobertas por
/// ApplyConfigurationsFromAssembly a partir deste assembly.
///
/// <see cref="OutboxMessages"/> é mapeado aqui (e excluído das migrations de Legal — ver
/// OutboxMessageMappingConfiguration) porque o OutboxInterceptor precisa gravar os eventos
/// de domínio dos agregados de Legal na MESMA transação do SaveChanges deste contexto
/// (ADR-008). A tabela pertence ao Platform, que é quem a cria e a processa.
/// </summary>
public sealed class LegalDbContext(DbContextOptions<LegalDbContext> options, ITenantContext tenantContext)
    : BaseDbContext(options, tenantContext)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Processo> Processos => Set<Processo>();

    public DbSet<Publication> Publications => Set<Publication>();

    public DbSet<PublicationSuggestion> PublicationSuggestions => Set<PublicationSuggestion>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}
