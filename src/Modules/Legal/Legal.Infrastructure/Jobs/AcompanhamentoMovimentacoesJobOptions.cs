namespace Advocacia.Modules.Legal.Infrastructure.Jobs;

/// <summary>
/// Seção "Jobs:AcompanhamentoMovimentacoes". DESLIGADO por padrão: ligar significa consultar os
/// portais dos tribunais todas as noites, por processo — decisão de cada ambiente (ADR-075).
/// </summary>
public sealed class AcompanhamentoMovimentacoesJobOptions
{
    public const string SectionName = "Jobs:AcompanhamentoMovimentacoes";

    public bool Enabled { get; init; }

    /// <summary>Cron de 5 campos. Padrão: 02:30, antes da captura de publicações (03:00).</summary>
    public string CronExpression { get; init; } = "30 2 * * *";

    /// <summary>Dias antes do último andamento capturado que cada sincronização revisita.</summary>
    public int JanelaDias { get; init; } = 3;

    /// <summary>
    /// Pausa entre processos. O juscraper já espaça as requisições a cada portal (1 s, no máximo 2
    /// simultâneas); a pausa aqui mantém o job como um usuário lento, não uma rajada.
    /// </summary>
    public int PausaEntreProcessosMs { get; init; } = 1500;

    /// <summary>Restringe a estes escritórios. Vazio (o normal) = todos com processo ativo ou suspenso.</summary>
    public IReadOnlyList<Guid> TenantIds { get; init; } = [];
}
