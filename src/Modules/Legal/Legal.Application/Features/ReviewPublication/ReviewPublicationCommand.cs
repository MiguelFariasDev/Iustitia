using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.ReviewPublication;

/// <summary>
/// Decisão do advogado sobre a publicação — a revisão humana obrigatória (ADR-012, Provimento CFOAB nº 271/2025)
/// que, quando há prazo, também o coloca na agenda. É UMA operação no servidor: o cálculo do vencimento, o
/// agendamento e a gravação da decisão não dependem de o navegador fazer três chamadas na ordem certa.
/// </summary>
/// <param name="Decision">"Approved" (aceita a sugestão da IA), "Corrected" (usa o prazo informado) ou "Rejected".</param>
/// <param name="DeadlineDays">Prazo em dias. Em "Corrected", o prazo que o advogado definiu; em "Approved", ignorado
/// (vale o sugerido); em "Rejected", ignorado.</param>
/// <param name="Notes">Observação do advogado. A sugestão original da IA é anexada automaticamente ao registro.</param>
[Audited("Reviewed", "Publication", EntityIdProperty = nameof(PublicationId), ActionFromProperty = nameof(Decision), DetailProperties = [nameof(Decision), nameof(DeadlineDays)])]
public sealed record ReviewPublicationCommand(
    Guid PublicationId,
    string Decision,
    int? DeadlineDays = null,
    string? Notes = null) : ICommand<ReviewPublicationResponse>;

/// <param name="Status">Novo status da publicação.</param>
/// <param name="Vencimento">Data do vencimento calculada; nula quando nenhum prazo foi agendado.</param>
/// <param name="EventId">Evento da agenda criado (ou o que já existia para a publicação).</param>
/// <param name="Notes">Como o prazo foi contado (publicação, início da contagem, recesso). É ESTIMATIVA.</param>
public sealed record ReviewPublicationResponse(
    string Status,
    DateOnly? Vencimento,
    Guid? EventId,
    int? Days,
    string? DeadlineKind,
    IReadOnlyList<string> Notes);
