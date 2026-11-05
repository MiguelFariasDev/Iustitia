using Advocacia.Modules.Atendimentos.Domain.Atendimentos;
using Advocacia.Modules.Atendimentos.Domain.Consulta;

namespace Advocacia.Modules.Atendimentos.Application.Abstractions;

public interface IConsultaSessaoRepository
{
    void Add(ConsultaSessao sessao);

    /// <summary>A sessão mais recente do atendimento, qualquer que seja o estado.</summary>
    Task<ConsultaSessao?> GetLatestByAtendimentoAsync(AtendimentoId atendimentoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Até quando este CLIENTE está bloqueado, se ainda estiver. O bloqueio é do cliente, não da
    /// sessão nem do atendimento: abrir outra conversa não o contorna.
    /// </summary>
    Task<DateTimeOffset?> GetBlockedUntilAsync(
        Guid tenantId, Guid clientId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// O maior número de falhas, desde <paramref name="since"/>, das sessões deste cliente que não
    /// terminaram verificadas nem bloqueadas. É o que impede zerar o contador recomeçando a sessão.
    /// </summary>
    Task<int> GetRecentFailuresAsync(
        Guid tenantId, Guid clientId, DateTimeOffset since, CancellationToken cancellationToken = default);
}
