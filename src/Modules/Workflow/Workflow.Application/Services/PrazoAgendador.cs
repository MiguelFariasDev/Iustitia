using Advocacia.BuildingBlocks.Application.Abstractions;
using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Workflow.Application.Abstractions;
using Advocacia.Modules.Workflow.Domain.Calendar;

namespace Advocacia.Modules.Workflow.Application.Services;

/// <summary>
/// Implementação de <see cref="IPrazoAgendador"/>: grava o prazo como evento de dia inteiro na agenda. Se já
/// existe prazo para a publicação, devolve o existente sem criar outro nem alterá-lo — o advogado pode ter
/// ajustado o evento depois, e uma repetição não deve desfazer isso.
/// </summary>
public sealed class PrazoAgendador(ICalendarEventRepository repository, IWorkflowUnitOfWork unitOfWork) : IPrazoAgendador
{
    public async Task<Result<Guid>> AgendarAsync(AgendarPrazoRequest request, CancellationToken cancellationToken = default)
    {
        var existente = await repository.GetBySourcePublicationAsync(request.TenantId, request.PublicationId, cancellationToken);
        if (existente is not null)
        {
            return Result.Success(existente.Id.Value);
        }

        var evento = CalendarEvent.CreateDeadlineFromPublication(
            request.TenantId, request.PublicationId, request.Titulo, request.Descricao,
            request.Vencimento, request.ProcessoId, request.ResponsavelUserId);
        if (evento.IsFailure)
        {
            return Result.Failure<Guid>(evento.Error);
        }

        repository.Add(evento.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(evento.Value.Id.Value);
    }
}
