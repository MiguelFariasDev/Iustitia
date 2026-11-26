using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Movimentacoes;

namespace Advocacia.Modules.Legal.Application.Abstractions;

public interface IProcessoMovimentacaoRepository : IRepository<ProcessoMovimentacao, ProcessoMovimentacaoId>
{
    /// <summary>Quais destes ids já estão gravados para o processo (uma consulta só).</summary>
    Task<IReadOnlySet<string>> ExternalIdsExistentesAsync(
        Guid processoId, IReadOnlyCollection<string> externalIds, CancellationToken cancellationToken = default);

    /// <summary>Resumo do que já foi capturado; nulo quando o processo nunca foi sincronizado.</summary>
    Task<CapturaExistente?> UltimaCapturaAsync(Guid processoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessoMovimentacao>> ListAsync(
        Guid processoId, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid processoId, CancellationToken cancellationToken = default);

    /// <summary>Andamentos capturados a partir de <paramref name="capturadasDesde"/> (os de um aviso pendente).</summary>
    Task<IReadOnlyList<ProcessoMovimentacao>> ListCapturadasDesdeAsync(
        Guid processoId, DateTimeOffset capturadasDesde, CancellationToken cancellationToken = default);
}

/// <param name="Fonte">Fonte do andamento gravado por último — a fonte em que o processo é acompanhado.</param>
/// <param name="UltimoAndamento">Data do andamento mais recente (nula se nenhum tiver data).</param>
public sealed record CapturaExistente(string Fonte, DateTimeOffset? UltimoAndamento);
