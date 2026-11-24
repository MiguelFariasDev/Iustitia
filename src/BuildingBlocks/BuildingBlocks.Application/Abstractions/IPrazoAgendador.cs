using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Porta para colocar na agenda o prazo que nasce da decisão do advogado sobre uma publicação. Existe em
/// BuildingBlocks porque o módulo Legal (dono da publicação) não pode depender do Workflow (dono da agenda);
/// a implementação vive em Workflow.Application (mesmo padrão de <see cref="IAlertPublisher"/>).
///
/// IDEMPOTENTE por (escritório, publicação): decidir de novo, repetir a chamada ou um retry de rede devolvem o
/// MESMO evento, nunca um segundo prazo. É o que torna seguro agendar ANTES de gravar a decisão: se a gravação
/// falhar, o retry não duplica.
/// </summary>
public interface IPrazoAgendador
{
    /// <returns>O id do evento da agenda (novo ou o que já existia para a publicação).</returns>
    Task<Result<Guid>> AgendarAsync(AgendarPrazoRequest request, CancellationToken cancellationToken = default);
}

/// <param name="TenantId">Escritório dono do prazo.</param>
/// <param name="PublicationId">Publicação que originou o prazo; chave de idempotência.</param>
/// <param name="ProcessoId">Processo ao qual a publicação pertence, quando vinculada.</param>
/// <param name="Titulo">Título do evento (ex.: "Prazo: Contestação — 0000000-00.0000.0.00.0000").</param>
/// <param name="Descricao">Detalhes do cálculo. Nunca o inteiro teor da publicação.</param>
/// <param name="Vencimento">Data do vencimento (dia inteiro, sem fuso).</param>
/// <param name="ResponsavelUserId">Quem recebe os lembretes: o responsável pelo processo.</param>
public sealed record AgendarPrazoRequest(
    Guid TenantId,
    Guid PublicationId,
    Guid? ProcessoId,
    string Titulo,
    string? Descricao,
    DateOnly Vencimento,
    Guid? ResponsavelUserId);
