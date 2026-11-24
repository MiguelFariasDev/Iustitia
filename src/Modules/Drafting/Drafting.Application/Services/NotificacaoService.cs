using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Domain.Pecas;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Drafting.Application.Services;

/// <summary>
/// Notificação ao término: alerta in-app (o mesmo canal do sino, que também chega por Realtime) e,
/// para o front, o polling de status. Não há SignalR nem e-mail neste projeto; ambos poderiam ser
/// acrescentados atrás desta mesma interface.
/// </summary>
public sealed class NotificacaoService(IAlertPublisher alertPublisher, ILogger<NotificacaoService> logger) : INotificacaoService
{
    public Task NotificarConclusaoAsync(LegalDocumentRequest request, CancellationToken cancellationToken = default) =>
        PublicarAsync(
            request,
            "Minuta pronta para revisão",
            $"Sua minuta de {PecaCatalogo.RotuloTipo(request.TipoPeca)} foi gerada. Revise e valide antes de usar.",
            cancellationToken);

    public Task NotificarFalhaAsync(LegalDocumentRequest request, CancellationToken cancellationToken = default) =>
        PublicarAsync(
            request,
            "Não foi possível gerar a minuta",
            $"A geração da minuta de {PecaCatalogo.RotuloTipo(request.TipoPeca)} falhou. Você pode tentar novamente.",
            cancellationToken);

    public void NotificarOperacao(string mensagem) =>
        // Nível Error: é o que o alarme da operação (Application Insights) observa. Não há canal de
        // "administrador global" na plataforma; quem opera a GPU é avisado por log/alerta de infra.
        logger.LogError("Operação de IA requer atenção: {Mensagem}", mensagem);

    private async Task PublicarAsync(
        LegalDocumentRequest request, string titulo, string descricao, CancellationToken cancellationToken)
    {
        try
        {
            // "Other": o único tipo genérico do catálogo de alertas.
            await alertPublisher.PublishAsync(
                new AlertRequest(request.TenantId, request.UsuarioId, "Other", titulo, descricao, request.Id.Value),
                cancellationToken);
        }
        catch (Exception exception)
        {
            // Avisar é conforto: se falhar, o status continua disponível por polling.
            logger.LogWarning(exception, "Peças: não foi possível notificar a solicitação {SolicitacaoId}.", request.Id.Value);
        }
    }
}
