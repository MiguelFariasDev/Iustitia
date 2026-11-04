using Advocacia.Modules.Legal.Domain.Processos;

namespace Advocacia.Modules.Legal.Application.Features.Processos;

/// <summary>Mapeamento manual pelo mesmo motivo de ClienteMapping: value objects com regra.</summary>
internal static class ProcessoMapping
{
    internal static ProcessoResponse ToResponse(this Processo processo) => new(
        processo.Id.Value,
        processo.CnjNumber.Value,
        processo.ClienteId,
        processo.ResponsavelUserId,
        processo.Area.ToString(),
        processo.Status.ToString(),
        processo.Foro.Tribunal,
        processo.Foro.Comarca,
        processo.Foro.Vara,
        processo.Assunto,
        processo.ParteContraria,
        processo.ValorCausa,
        processo.DataDistribuicao,
        processo.Observacoes,
        processo.GeraCaptura,
        processo.CreatedAt);

    internal static ProcessoListItem ToListItem(this Processo processo) => new(
        processo.Id.Value,
        processo.CnjNumber.Value,
        processo.ClienteId,
        processo.Area.ToString(),
        processo.Status.ToString(),
        processo.Foro.Tribunal,
        processo.ParteContraria,
        processo.DataDistribuicao);
}
