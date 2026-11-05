using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.BuildingBlocks.Domain.Specifications;
using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using NSubstitute;

namespace Advocacia.Modules.Atendimentos.Application.UnitTests;

/// <summary>
/// Repositório em memória: faz os handlers exercitarem o domínio de verdade (a máquina de
/// estados), em vez de verificar chamadas a mocks.
/// </summary>
internal sealed class InMemoryAtendimentoRepository : IAtendimentoRepository
{
    public List<Atendimento> Atendimentos { get; } = [];

    public List<AtendimentoMensagem> Mensagens { get; } = [];

    public List<AtendimentoAuditoria> Auditoria { get; } = [];

    public Task<Atendimento?> GetByIdAsync(AtendimentoId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Atendimentos.FirstOrDefault(a => a.Id == id));

    public Task<Atendimento?> GetOpenByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Atendimentos.FirstOrDefault(a => a.PhoneNumber == phoneNumber && a.IsOpen));

    public Task<AtendimentoMensagem?> GetMensagemByExternalIdAsync(string externalId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Mensagens.FirstOrDefault(m => m.ExternalId == externalId));

    public void Add(Atendimento entity) => Atendimentos.Add(entity);

    public void Update(Atendimento entity)
    {
    }

    public void Remove(Atendimento entity) => Atendimentos.Remove(entity);

    public void AddMensagem(AtendimentoMensagem mensagem) => Mensagens.Add(mensagem);

    public void AddAuditoria(AtendimentoAuditoria auditoria) => Auditoria.Add(auditoria);

    public Task<IReadOnlyList<AtendimentoListItem>> ListAsync(
        AtendimentoFilter filter, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AtendimentoListItem>>(
            Atendimentos.Skip(skip).Take(take).Select(a => new AtendimentoListItem(a, null)).ToList());

    public Task<int> CountAsync(AtendimentoFilter filter, CancellationToken cancellationToken = default) =>
        Task.FromResult(Atendimentos.Count);

    public Task<IReadOnlyList<AtendimentoMensagem>> ListMensagensAsync(
        AtendimentoId atendimentoId, DateTimeOffset? before, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AtendimentoMensagem>>(Mensagens
            .Where(m => m.AtendimentoId == atendimentoId && (before == null || m.CreatedAt < before))
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToList());

    // Não usados pelos handlers de Atendimento.
    public Task<Atendimento?> FirstOrDefaultAsync(ISpecification<Atendimento> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Atendimento>> ListAsync(ISpecification<Atendimento> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<int> CountAsync(ISpecification<Atendimento> specification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class FakeUnitOfWork : IAtendimentoUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal static class Doubles
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly Guid LawyerId = Guid.NewGuid();
    public static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    public static ICurrentUser Lawyer(Guid? userId = null, Guid? tenantId = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(userId ?? LawyerId);
        user.TenantId.Returns(tenantId ?? TenantId);
        return user;
    }

    /// <summary>O Bot não tem usuário: só tenant, vindo da chave de API.</summary>
    public static ICurrentUser BotCaller(Guid? tenantId = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns((Guid?)null);
        user.TenantId.Returns(tenantId ?? TenantId);
        return user;
    }

    public static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return clock;
    }

    public static IBotGateway GatewayReturning(Result<string?> result)
    {
        var gateway = Substitute.For<IBotGateway>();
        gateway.SendTextAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(result);
        return gateway;
    }
}
