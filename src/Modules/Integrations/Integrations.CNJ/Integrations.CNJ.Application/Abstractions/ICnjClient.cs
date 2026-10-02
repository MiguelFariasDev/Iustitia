using Advocacia.BuildingBlocks.Domain.Results;
using Advocacia.Modules.Integrations.CNJ.Application.Models;

namespace Advocacia.Modules.Integrations.CNJ.Application.Abstractions;

/// <summary>
/// Porta de saída para o DJEN (Diário de Justiça Eletrônico Nacional) do CNJ. A
/// implementação real (HTTP + Polly) fica em Integrations.CNJ.Infrastructure; consumidores
/// (ex.: CnjCaptureJob, no módulo Legal) dependem só desta interface.
///
/// Falhas de integração são devolvidas como <see cref="Result"/> de erro (faixa
/// INTEGRATION do catálogo), nunca como exception — quem decide se relança para o Hangfire
/// aplicar retry é o job, não o cliente (ver ADR-007).
/// </summary>
public interface ICnjClient
{
    Task<Result<CnjPublicationPage>> GetPublicationsAsync(
        CnjPublicationQuery query, CancellationToken cancellationToken = default);

    /// <summary>Verificação de reachability usada pelo health check (ver CnjHealthCheck).</summary>
    Task<Result> PingAsync(CancellationToken cancellationToken = default);
}
