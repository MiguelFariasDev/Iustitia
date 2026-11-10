using Advocacia.BuildingBlocks.Domain.Results;

namespace Advocacia.Modules.Atendimentos.Application.Abstractions;

/// <summary>
/// Saída para o Bot. O Core nunca fala com a Meta: toda mensagem do advogado passa por aqui
/// e o Bot é quem a entrega ao WhatsApp. O resultado traz o <c>wamid</c> devolvido pela Meta.
/// </summary>
public interface IBotGateway
{
    Task<Result<string?>> SendTextAsync(
        Guid tenantId,
        Guid atendimentoId,
        Guid mensagemId,
        string phoneNumber,
        string body,
        CancellationToken cancellationToken = default);
}
