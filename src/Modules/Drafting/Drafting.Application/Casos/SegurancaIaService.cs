using Advocacia.BuildingBlocks.Domain.Time;
using Advocacia.Modules.Drafting.Application.Abstractions;
using Advocacia.Modules.Drafting.Application.Services;
using Advocacia.Modules.Drafting.Domain.Seguranca;
using Microsoft.Extensions.Logging;

namespace Advocacia.Modules.Drafting.Application.Casos;

public sealed class SegurancaIaService(
    IEventoSegurancaRepository repositorio,
    IDraftingUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<SegurancaIaService> logger) : ISegurancaIaService
{
    public async Task RegistrarAsync(
        Guid tenantId, Guid? usuarioId, string tipo, string origem, string? conteudo, CancellationToken cancellationToken = default)
    {
        // Nível Warning: é o que o alarme observa. Só tipo e ids — o texto suspeito nunca vai para o log.
        logger.LogWarning(
            "Segurança IA: {Tipo} em {Origem} (tenant {TenantId}, usuário {UsuarioId}).", tipo, origem, tenantId, usuarioId);

        repositorio.Add(new EventoSegurancaIa(
            tenantId, usuarioId, tipo, origem, conteudo is null ? null : TextoHash.DoTexto(conteudo), clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
