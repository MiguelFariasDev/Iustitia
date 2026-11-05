namespace Advocacia.Modules.Legal.Infrastructure.Jobs;

/// <summary>
/// Seção "Jobs:PublicationClassification" do appsettings. O agendamento
/// (Enabled/CronExpression) é lido por <c>LegalRecurringJobsRegistrar</c>; o restante, pelo job.
/// </summary>
public sealed class PublicationClassificationJobOptions
{
    public const string SectionName = "Jobs:PublicationClassification";

    public bool Enabled { get; init; } = true;

    /// <summary>Cron de 5 campos. Padrão: a cada 15 minutos.</summary>
    public string CronExpression { get; init; } = "*/15 * * * *";

    /// <summary>
    /// Máximo de publicações classificadas por escritório em cada execução. Limita o gasto de
    /// uma execução (cada publicação são até duas chamadas pagas) e impede que um escritório
    /// com backlog grande monopolize a execução dos demais.
    /// </summary>
    public int BatchSizePerTenant { get; init; } = 20;

    /// <summary>
    /// Quantas respostas inválidas de uma mesma publicação são toleradas antes de o job deixar
    /// de tentá-la. Cada tentativa é cobrada; passado o limite a publicação segue na fila de
    /// revisão SEM sugestão, para o advogado tratar manualmente.
    /// </summary>
    public int MaxInvalidResponseAttempts { get; init; } = 3;

    /// <summary>Restringe a execução a estes escritórios. Vazio (o normal) = todos com trabalho pendente.</summary>
    public IReadOnlyList<Guid> TenantIds { get; init; } = [];
}
