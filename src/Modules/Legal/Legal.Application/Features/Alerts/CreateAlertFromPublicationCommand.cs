using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.Alerts;

/// <summary>
/// Cria o alerta in-app de uma publicação recém-capturada para o usuário informado (hoje, o
/// responsável pelo processo). Comando de uso interno — o job de captura o dispara —, por isso
/// carrega o UserId em vez de resolvê-lo de <c>ICurrentUser</c>.
/// </summary>
public sealed record CreateAlertFromPublicationCommand(Guid PublicationId, Guid UserId)
    : ICommand<CreateAlertFromPublicationResponse>;

/// <param name="Created">false quando o alerta já existia (reexecução idempotente).</param>
public sealed record CreateAlertFromPublicationResponse(bool Created);
