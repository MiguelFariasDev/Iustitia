using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Application.Messaging;
using Advocacia.BuildingBlocks.Domain.Errors;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Legal.Application.Abstractions;
using Advocacia.Modules.Legal.Domain.Prazos;
using Advocacia.Modules.Legal.Domain.Processos;
using Advocacia.Modules.Legal.Domain.Publications;
using Advocacia.Modules.Legal.Domain.Suggestions;

namespace Advocacia.Modules.Legal.Application.Features.ReviewPublication;

/// <summary>
/// Ordem das etapas (importa para a segurança do prazo):
/// <list type="number">
/// <item>valida a transição de status em memória (nada gravado ainda);</item>
/// <item>calcula o vencimento com a calculadora forense, a partir da disponibilização;</item>
/// <item>AGENDA o prazo — idempotente por publicação;</item>
/// <item>só então grava a decisão.</item>
/// </list>
/// Se a gravação da decisão falhar depois do agendamento, o advogado repete a ação e o prazo não duplica. O
/// contrário (decisão gravada, prazo perdido) deixaria uma publicação "tratada" sem o prazo na agenda — o pior
/// erro possível neste produto.
/// </summary>
public sealed class ReviewPublicationHandler(
    IPublicationRepository publicationRepository,
    IPublicationSuggestionRepository suggestionRepository,
    IProcessoRepository processoRepository,
    IPrazoAgendador prazoAgendador,
    ILegalUnitOfWork unitOfWork) : ICommandHandler<ReviewPublicationCommand, ReviewPublicationResponse>
{
    /// <summary>Horário de Brasília: a data de publicação é a do calendário do escritório, não a UTC.</summary>
    private static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);

    public async Task<Result<ReviewPublicationResponse>> Handle(ReviewPublicationCommand request, CancellationToken cancellationToken)
    {
        var publication = await publicationRepository.GetByIdAsync(PublicationId.From(request.PublicationId), cancellationToken);
        if (publication is null)
        {
            return Result.Failure<ReviewPublicationResponse>(ErrorFactory.From(ErrorCode.PUBLICATION_NOT_FOUND));
        }

        if (!Enum.TryParse<PublicationStatus>(request.Decision, ignoreCase: false, out var status)
            || status is not (PublicationStatus.Approved or PublicationStatus.Corrected or PublicationStatus.Rejected))
        {
            return Result.Failure<ReviewPublicationResponse>(ErrorFactory.From(ErrorCode.VALIDATION_INVALID_ENUM, nameof(request.Decision)));
        }

        var sugestao = await suggestionRepository.GetByPublicationIdAsync(publication.TenantId, publication.Id, cancellationToken);

        var (dias, tipo) = DecidirPrazo(status, request.DeadlineDays, sugestao);

        // 1) Transição válida? (só em memória: a publicação ainda não foi salva)
        var transicao = publication.ChangeStatus(status, MontarNota(request, sugestao, dias, tipo));
        if (transicao.IsFailure)
        {
            return Result.Failure<ReviewPublicationResponse>(transicao.Error);
        }

        DateOnly? vencimento = null;
        Guid? eventId = null;
        IReadOnlyList<string> notas = [];

        if (dias is { } diasDoPrazo)
        {
            // 2) Calcula.
            var publicadaEm = DateOnly.FromDateTime(publication.PublishedAt.Value.ToOffset(Brasilia).DateTime);
            var calculo = CalculadoraPrazo.Calcular(publicadaEm, diasDoPrazo, tipo, ReferenciaDoPrazo.Disponibilizacao);
            vencimento = calculo.Vencimento;
            notas = calculo.Observacoes;

            // 3) Agenda (idempotente).
            var processo = publication.ProcessId is { } processoId
                ? await processoRepository.GetByIdAsync(ProcessoId.From(processoId), cancellationToken)
                : null;

            var agendamento = await prazoAgendador.AgendarAsync(
                new AgendarPrazoRequest(
                    publication.TenantId,
                    publication.Id.Value,
                    publication.ProcessId,
                    MontarTitulo(sugestao, publication),
                    MontarDescricao(diasDoPrazo, tipo, publicadaEm, calculo),
                    calculo.Vencimento,
                    processo?.ResponsavelUserId),
                cancellationToken);
            if (agendamento.IsFailure)
            {
                return Result.Failure<ReviewPublicationResponse>(agendamento.Error);
            }

            eventId = agendamento.Value;
        }

        // 4) Grava a decisão.
        publicationRepository.Update(publication);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ReviewPublicationResponse(
            status.ToString(), vencimento, eventId, dias, dias is null ? null : tipo == TipoContagem.DiasUteis ? "business" : "calendar", notas));
    }

    /// <summary>Aceitar usa o prazo sugerido; corrigir, o que o advogado informou; rejeitar não agenda nada.</summary>
    private static (int? Dias, TipoContagem Tipo) DecidirPrazo(PublicationStatus status, int? informado, PublicationSuggestion? sugestao)
    {
        var tipoSugerido = sugestao?.DeadlineType == DeadlineType.Calendar ? TipoContagem.DiasCorridos : TipoContagem.DiasUteis;

        return status switch
        {
            PublicationStatus.Approved when sugestao?.SuggestedDeadlineDays is > 0 => (sugestao.SuggestedDeadlineDays, tipoSugerido),
            PublicationStatus.Corrected when informado is > 0 => (informado, tipoSugerido),
            _ => (null, tipoSugerido),
        };
    }

    private static string MontarTitulo(PublicationSuggestion? sugestao, Publication publication)
    {
        var ato = string.IsNullOrWhiteSpace(sugestao?.ActType) ? "Providência" : sugestao.ActType;
        var titulo = $"Prazo: {ato} — {publication.CnjNumber.Value}";

        return titulo.Length <= 200 ? titulo : titulo[..200];
    }

    private static string MontarDescricao(int dias, TipoContagem tipo, DateOnly publicadaEm, ResultadoPrazo calculo)
    {
        var unidade = tipo == TipoContagem.DiasUteis ? "dias úteis" : "dias corridos";

        // Só a referência: o inteiro teor da publicação nunca vai para a descrição do evento.
        return $"{dias} {unidade} a partir da disponibilização em {publicadaEm:dd/MM/yyyy}. "
               + $"Vencimento calculado pelo sistema: {calculo.Vencimento:dd/MM/yyyy}. "
               + "É uma ESTIMATIVA pelo calendário forense nacional (sem feriados locais): confira antes de contar com a data.";
    }

    /// <summary>Registro auditável do que a IA sugeriu e do que o advogado decidiu (nunca o texto da publicação).</summary>
    private static string MontarNota(ReviewPublicationCommand request, PublicationSuggestion? sugestao, int? diasDecididos, TipoContagem tipo)
    {
        var partes = new List<string>();

        if (sugestao is not null)
        {
            var sugerido = sugestao.SuggestedDeadlineDays is { } d ? $"{d} dias" : "sem prazo";
            partes.Add($"IA sugeriu: {sugestao.ActType}, {sugerido}, confiança {sugestao.Confidence}%.");
        }

        partes.Add(diasDecididos is { } dias
            ? $"Decisão: {request.Decision} com prazo de {dias} {(tipo == TipoContagem.DiasUteis ? "dias úteis" : "dias corridos")}."
            : $"Decisão: {request.Decision} sem prazo na agenda.");

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            partes.Add(request.Notes.Trim());
        }

        return string.Join(' ', partes);
    }
}
