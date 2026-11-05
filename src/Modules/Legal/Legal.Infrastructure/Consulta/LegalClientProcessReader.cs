using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advocacia.Modules.Legal.Infrastructure.Consulta;

/// <summary>
/// Resumo do andamento para o cliente final. Decisões, todas pensadas para NÃO expor demais:
/// <list type="bullet">
/// <item>só título, fase e último andamento chegam ao cliente — a consulta seleciona só estas colunas,
/// então valor da causa, parte contrária, observações e peças nem são lidos;</item>
/// <item>o "último andamento" vem apenas de publicações que o advogado JÁ REVISOU (Approved ou
/// Corrected). Publicação pendente tem classificação automática ainda não confirmada, e a revisão
/// humana é obrigatória (ADR-012): o cliente não fica sabendo de uma sentença por um bot antes de o
/// advogado ter olhado. Do que foi revisado sai só o TIPO e a DATA — nunca o inteiro teor;</item>
/// <item>"fase" é a situação cadastrada do processo: o sistema não modela fases processuais.</item>
/// </list>
/// </summary>
public sealed class LegalClientProcessReader(LegalDbContext dbContext) : IClientProcessReader
{
    private const string NoMovementYet = "Ainda não há movimentações confirmadas pelo escritório.";

    public async Task<IReadOnlyList<ClientProcessSummary>> ListAsync(
        Guid tenantId, Guid clientId, int max, CancellationToken cancellationToken = default)
    {
        var rows = await ProcessesOf(tenantId, clientId)
            .OrderByDescending(processo => processo.UpdatedAt ?? processo.CreatedAt)
            .Take(max)
            .Select(processo => new ProcessRow(
                processo.Id, processo.CnjNumber, processo.Assunto, processo.Area, processo.Status,
                processo.UpdatedAt ?? processo.CreatedAt))
            .ToListAsync(cancellationToken);

        return await ToSummariesAsync(tenantId, rows, cancellationToken);
    }

    public async Task<ClientProcessSummary?> GetAsync(
        Guid tenantId, Guid clientId, Guid processId, CancellationToken cancellationToken = default)
    {
        var id = ProcessoId.From(processId);

        // O processo só existe para esta consulta se for DESTE cliente neste escritório.
        var rows = await ProcessesOf(tenantId, clientId)
            .Where(processo => processo.Id == id)
            .Select(processo => new ProcessRow(
                processo.Id, processo.CnjNumber, processo.Assunto, processo.Area, processo.Status,
                processo.UpdatedAt ?? processo.CreatedAt))
            .ToListAsync(cancellationToken);

        return (await ToSummariesAsync(tenantId, rows, cancellationToken)).FirstOrDefault();
    }

    private IQueryable<Processo> ProcessesOf(Guid tenantId, Guid clientId) =>
        dbContext.Processos.Where(processo =>
            processo.TenantId == tenantId
            && processo.ClienteId == clientId
            && processo.Status != StatusProcesso.Arquivado);

    private async Task<IReadOnlyList<ClientProcessSummary>> ToSummariesAsync(
        Guid tenantId, List<ProcessRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(row => row.Id.Value).ToList();

        var movements = await dbContext.Publications
            .Where(publication => publication.TenantId == tenantId
                && publication.ProcessId != null
                && ids.Contains(publication.ProcessId.Value)
                && (publication.Status == PublicationStatus.Approved || publication.Status == PublicationStatus.Corrected))
            .OrderByDescending(publication => publication.PublishedOn)
            .ThenByDescending(publication => publication.CapturedAt)
            .Select(publication => new { ProcessId = publication.ProcessId!.Value, publication.Type, publication.PublishedOn })
            .Take(2000)
            .ToListAsync(cancellationToken);

        var latestByProcess = movements.GroupBy(movement => movement.ProcessId).ToDictionary(g => g.Key, g => g.First());

        return rows.Select(row =>
        {
            var hasMovement = latestByProcess.TryGetValue(row.Id.Value, out var movement);

            var updatedAt = hasMovement
                ? Max(row.UpdatedAt, new DateTimeOffset(movement!.PublishedOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))
                : row.UpdatedAt;

            return new ClientProcessSummary(
                row.Id.Value,
                Title(row),
                Phase(row.Status),
                hasMovement ? DescribeMovement(movement!.Type, movement.PublishedOn) : NoMovementYet,
                updatedAt);
        }).ToList();
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;

    private static string Title(ProcessRow row)
    {
        var subject = string.IsNullOrWhiteSpace(row.Assunto) ? $"Processo {AreaLabel(row.Area)}" : row.Assunto.Trim();

        // O número ajuda o cliente a reconhecer o processo; é dado público e já é dele.
        return $"{subject} · {row.CnjNumber.Value}";
    }

    internal static string Phase(StatusProcesso status) => status switch
    {
        StatusProcesso.Ativo => "Em andamento",
        StatusProcesso.Suspenso => "Suspenso",
        StatusProcesso.Encerrado => "Encerrado",
        _ => "Em análise pelo escritório",
    };

    internal static string DescribeMovement(PublicationType? type, DateOnly publishedOn)
    {
        var kind = type switch
        {
            PublicationType.Despacho => "Despacho do juiz",
            PublicationType.Decisao => "Decisão judicial",
            PublicationType.Intimacao => "Intimação",
            PublicationType.Sentenca => "Sentença",
            PublicationType.Acordao => "Acórdão (decisão do tribunal)",
            _ => "Movimentação processual",
        };

        return $"{kind} publicada em {publishedOn:dd/MM/yyyy}.";
    }

    private static string AreaLabel(AreaDireito area) => area switch
    {
        AreaDireito.Civel => "Cível",
        AreaDireito.Trabalhista => "Trabalhista",
        AreaDireito.Criminal => "Criminal",
        AreaDireito.Tributario => "Tributário",
        AreaDireito.Previdenciario => "Previdenciário",
        AreaDireito.Familia => "de Família",
        AreaDireito.Consumidor => "do Consumidor",
        AreaDireito.Empresarial => "Empresarial",
        AreaDireito.Administrativo => "Administrativo",
        AreaDireito.Ambiental => "Ambiental",
        _ => "Judicial",
    };

    private sealed record ProcessRow(
        ProcessoId Id, Domain.Shared.CNJNumber CnjNumber, string? Assunto, AreaDireito Area, StatusProcesso Status, DateTimeOffset UpdatedAt);
}
