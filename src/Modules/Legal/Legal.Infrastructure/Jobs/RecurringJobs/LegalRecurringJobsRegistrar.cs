using Advocacia.Modules.Legal.Infrastructure.Jobs;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Advocacia.Modules.Legal.Infrastructure.Jobs.RecurringJobs;

/// <summary>
/// Registra os jobs recorrentes do módulo Legal no Hangfire, a partir da seção "Jobs" do
/// appsettings. Chamado uma vez na inicialização do Worker, ao lado do
/// RecurringJobsRegistrar do Platform (ver Hosts/Worker/Program.cs) — cada módulo registra
/// os seus, sem o Platform precisar conhecê-los (ver ADR-001/ADR-017).
///
/// Um job desabilitado é REMOVIDO do storage, não apenas ignorado: sem isso, desligar
/// Jobs:CnjCapture:Enabled deixaria o agendamento anterior ativo no Hangfire para sempre.
/// </summary>
public sealed class LegalRecurringJobsRegistrar(
    IRecurringJobManager recurringJobManager,
    IOptions<CnjCaptureJobOptions> cnjCaptureOptions,
    IOptions<PublicationClassificationJobOptions> classificationOptions)
{
    public const string CnjCaptureJobId = "cnj-capture";

    public const string PublicationClassificationJobId = "publication-classification";

    public void RegisterAll()
    {
        var options = cnjCaptureOptions.Value;

        if (options.Enabled)
        {
            recurringJobManager.AddOrUpdate<CnjCaptureJob>(
                CnjCaptureJobId,
                job => job.ExecuteAsync(CancellationToken.None),
                options.CronExpression);
        }
        else
        {
            recurringJobManager.RemoveIfExists(CnjCaptureJobId);
        }

        var classification = classificationOptions.Value;

        if (classification.Enabled)
        {
            recurringJobManager.AddOrUpdate<PublicationClassificationJob>(
                PublicationClassificationJobId,
                job => job.ExecuteAsync(CancellationToken.None),
                classification.CronExpression);
        }
        else
        {
            recurringJobManager.RemoveIfExists(PublicationClassificationJobId);
        }
    }
}
