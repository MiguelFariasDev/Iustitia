using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.GetPublicationsSummary;

/// <summary>
/// Contadores agregados das publicações do escritório. Serve o cartão do painel e o badge de
/// pendências da navegação — os dois precisam dos mesmos números, e pedi-los por aqui evita
/// que o cliente baixe uma listagem inteira só para contar.
/// </summary>
public sealed record GetPublicationsSummaryQuery : IQuery<GetPublicationsSummaryResponse>;
