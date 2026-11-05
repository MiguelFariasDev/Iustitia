namespace Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;

/// <summary>Consultas de leitura sobre ai_usage_logs, para quem precisa decidir com base no histórico.</summary>
public interface IAnthropicUsageQuery
{
    /// <summary>
    /// Quantos registros de uma finalidade falharam com um código de erro, por objeto de negócio.
    /// Objetos sem nenhuma falha ficam fora do dicionário. Usado, por exemplo, para parar de
    /// reprocessar (e pagar de novo) uma publicação cuja resposta o modelo nunca acerta.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountFailuresByReferenceAsync(
        Guid tenantId,
        string purpose,
        string errorCode,
        IReadOnlyCollection<Guid> referenceIds,
        CancellationToken cancellationToken = default);
}
