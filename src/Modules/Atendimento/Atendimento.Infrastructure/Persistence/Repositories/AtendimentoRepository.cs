using Advocacia.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Advocacia.Modules.Atendimentos.Application.Abstractions;
using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Atendimentos.Infrastructure.Persistence.Repositories;

public sealed class AtendimentoRepository(AtendimentoDbContext dbContext)
    : Repository<Atendimento, AtendimentoId>(dbContext), IAtendimentoRepository
{
    private const int PreviewColumnLength = 100;

    public Task<Atendimento?> GetOpenByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(
            atendimento => atendimento.PhoneNumber == phoneNumber && atendimento.Status != AtendimentoStatus.Closed,
            cancellationToken);

    public Task<AtendimentoMensagem?> GetMensagemByExternalIdAsync(
        string externalId, CancellationToken cancellationToken = default) =>
        dbContext.Mensagens.FirstOrDefaultAsync(mensagem => mensagem.ExternalId == externalId, cancellationToken);

    public void AddMensagem(AtendimentoMensagem mensagem) => dbContext.Mensagens.Add(mensagem);

    public void AddAuditoria(AtendimentoAuditoria auditoria) => dbContext.Auditoria.Add(auditoria);

    public async Task<IReadOnlyList<AtendimentoListItem>> ListAsync(
        AtendimentoFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        // A fila prioriza quem espera um humano; encerrados vão para o fim.
        var rows = await Filtered(filter)
            .OrderBy(atendimento =>
                atendimento.Status == AtendimentoStatus.PendingHuman ? 0
                : atendimento.Status == AtendimentoStatus.Human ? 1
                : atendimento.Status == AtendimentoStatus.Bot ? 2
                : 3)
            .ThenByDescending(atendimento => atendimento.LastMessageAt)
            .Skip(skip)
            .Take(take)
            .Select(atendimento => new
            {
                Atendimento = atendimento,
                Preview = dbContext.Mensagens
                    .Where(mensagem => mensagem.AtendimentoId == atendimento.Id)
                    .OrderByDescending(mensagem => mensagem.CreatedAt)
                    .Select(mensagem => mensagem.Body.Substring(0, PreviewColumnLength))
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new AtendimentoListItem(row.Atendimento, row.Preview)).ToList();
    }

    public Task<int> CountAsync(AtendimentoFilter filter, CancellationToken cancellationToken = default) =>
        Filtered(filter).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<AtendimentoMensagem>> ListMensagensAsync(
        AtendimentoId atendimentoId, DateTimeOffset? before, int take, CancellationToken cancellationToken = default) =>
        await dbContext.Mensagens
            .Where(mensagem => mensagem.AtendimentoId == atendimentoId && (before == null || mensagem.CreatedAt < before))
            .OrderByDescending(mensagem => mensagem.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    private IQueryable<Atendimento> Filtered(AtendimentoFilter filter)
    {
        var query = DbSet.AsQueryable();

        if (filter.Status is { } status)
        {
            query = query.Where(atendimento => atendimento.Status == status);
        }

        if (filter.AssignedUserId is { } assignedUserId)
        {
            query = query.Where(atendimento => atendimento.AssignedUserId == assignedUserId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search;
            var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
            var pattern = $"%{Escape(term)}%";

            // Número: contém os dígitos digitados (quem busca "9999" quer achar o final).
            // Nome: ILIKE sobre o nome do perfil do WhatsApp.
            query = digits.Length >= 3
                ? query.Where(atendimento =>
                    atendimento.PhoneNumber.Contains(digits)
                    || (atendimento.ContactName != null && EF.Functions.ILike(atendimento.ContactName, pattern)))
                : query.Where(atendimento =>
                    atendimento.ContactName != null && EF.Functions.ILike(atendimento.ContactName, pattern));
        }

        return query;
    }

    private static string Escape(string term) =>
        term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
