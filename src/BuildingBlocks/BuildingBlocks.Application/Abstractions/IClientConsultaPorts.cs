namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// O que o cliente final vê de um processo seu quando pergunta pelo andamento no WhatsApp. É de
/// propósito MÍNIMO e já em linguagem simples: título, fase e último andamento. Peças, valores,
/// parte contrária, notas internas e o texto das publicações NÃO existem neste tipo — o que não
/// está aqui não tem como vazar.
/// </summary>
public sealed record ClientProcessSummary(
    Guid ProcessId, string Title, string Phase, string LastMovement, DateTimeOffset UpdatedAt);

/// <summary>
/// Identidade do cliente cadastrado, para a verificação feita no Core. Vive em BuildingBlocks e é
/// implementada por Legal.Infrastructure: o módulo Atendimento precisa saber QUEM é o dono de um
/// telefone, mas não pode depender do módulo Legal (ADR-055) — mesmo desenho de <c>IAlertPublisher</c>.
/// </summary>
public interface IClientIdentityDirectory
{
    /// <summary>
    /// O cliente ATIVO do escritório dono do telefone do WhatsApp. <c>null</c> quando não há nenhum
    /// <b>ou quando há mais de um</b>: um telefone compartilhado não identifica ninguém, e adivinhar
    /// qual cliente é o certo seria entregar dados de uma pessoa a outra.
    /// </summary>
    Task<Guid?> FindActiveClientByPhoneAsync(Guid tenantId, string whatsAppPhone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Se o documento (só dígitos) é o do cliente. Devolve apenas sim ou não: o documento cadastrado
    /// nunca sai de Legal por esta porta, e a comparação não vaza por tempo.
    /// </summary>
    Task<bool> DocumentMatchesAsync(Guid tenantId, Guid clientId, string documentDigits, CancellationToken cancellationToken = default);
}

/// <summary>Leitura dos processos de UM cliente, para a consulta de andamento.</summary>
public interface IClientProcessReader
{
    /// <summary>Processos do cliente, do mais recentemente movimentado para o mais antigo. Exclui os arquivados.</summary>
    Task<IReadOnlyList<ClientProcessSummary>> ListAsync(
        Guid tenantId, Guid clientId, int max, CancellationToken cancellationToken = default);

    /// <summary>
    /// Um processo, SOMENTE se pertencer a este cliente neste escritório; caso contrário <c>null</c>,
    /// indistinguível de "não existe". Quem chama não precisa (e não deve) confiar só no id.
    /// </summary>
    Task<ClientProcessSummary?> GetAsync(
        Guid tenantId, Guid clientId, Guid processId, CancellationToken cancellationToken = default);
}
