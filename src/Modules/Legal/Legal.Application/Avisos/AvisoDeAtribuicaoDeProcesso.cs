using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Processos;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Legal.Application.Avisos;

/// <summary>
/// Avisa cada advogado de que um processo foi atribuído a ele (cadastro, ou ao ser incluído na equipe do
/// processo depois). Alerta in-app pela porta <see cref="IAlertPublisher"/>; só o número do processo vai no
/// texto — partes e assunto são sigilosos e o alerta trafega pelo Realtime.
///
/// Quem fez a atribuição não é avisado de si mesmo. Falha ao avisar nunca desfaz o cadastro: o processo já
/// está salvo e o alerta é idempotente por (usuário, processo), então a próxima atribuição tenta de novo.
/// </summary>
internal static class AvisoDeAtribuicaoDeProcesso
{
    internal const string TipoDoAlerta = "Other";

    /// <summary>Os advogados que passaram a atuar no processo: os de agora que não estavam antes.</summary>
    internal static IReadOnlyList<Guid> NovosAdvogados(IEnumerable<Guid> antes, Processo processo) =>
        processo.TodosOsAdvogados.Except(antes).ToList();

    internal static async Task AvisarAsync(
        IAlertPublisher alertPublisher,
        ILogger logger,
        Processo processo,
        IEnumerable<Guid> destinatarios,
        Guid? autor,
        CancellationToken cancellationToken)
    {
        foreach (var userId in destinatarios.Where(id => id != autor).Distinct())
        {
            try
            {
                await alertPublisher.PublishAsync(
                    new AlertRequest(
                        processo.TenantId,
                        userId,
                        TipoDoAlerta,
                        $"Novo processo para você: {processo.CnjNumber.Value}",
                        processo.ResponsavelUserId == userId
                            ? "Você é o advogado responsável por este processo."
                            : "Você foi incluído como advogado neste processo.",
                        processo.Id.Value),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Aviso de processo {ProcessoId} não enviado ao usuário {UserId}.", processo.Id.Value, userId);
            }
        }
    }
}
