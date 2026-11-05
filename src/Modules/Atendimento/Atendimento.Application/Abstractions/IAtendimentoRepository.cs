using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;

namespace Advocacia.Modules.Atendimentos.Application.Abstractions;

public sealed record AtendimentoFilter(
    AtendimentoStatus? Status,
    Guid? AssignedUserId,
    string? Search);

/// <summary>Linha da fila do Inbox: o atendimento mais um trecho da última mensagem.</summary>
public sealed record AtendimentoListItem(Atendimento Atendimento, string? LastMessagePreview);

/// <remarks>
/// NÃO chame <c>Update</c> neste módulo. A entidade vem rastreada de <c>GetByIdAsync</c>, então o
/// EF já detecta sozinho as colunas alteradas; <c>Update</c> marcaria TODAS como modificadas e o
/// <c>UPDATE</c> passaria a incluir tenant_id, phone_number e created_at — colunas para as quais
/// o role da aplicação não tem permissão (ver 014_atendimentos.sql). Coberto por
/// AtendimentoAsAppUserTests.
/// </remarks>
public interface IAtendimentoRepository : IRepository<Atendimento, AtendimentoId>
{
    /// <summary>O atendimento não encerrado deste telefone, se houver (no máximo um — índice único parcial).</summary>
    Task<Atendimento?> GetOpenByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>Deduplicação de webhook: a Meta reentrega a mesma mensagem com o mesmo wamid.</summary>
    Task<AtendimentoMensagem?> GetMensagemByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    void AddMensagem(AtendimentoMensagem mensagem);

    void AddAuditoria(AtendimentoAuditoria auditoria);

    Task<IReadOnlyList<AtendimentoListItem>> ListAsync(
        AtendimentoFilter filter, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountAsync(AtendimentoFilter filter, CancellationToken cancellationToken = default);

    /// <summary>As mensagens mais recentes antes de <paramref name="before"/>, da mais nova para a mais antiga.</summary>
    Task<IReadOnlyList<AtendimentoMensagem>> ListMensagensAsync(
        AtendimentoId atendimentoId, DateTimeOffset? before, int take, CancellationToken cancellationToken = default);
}
