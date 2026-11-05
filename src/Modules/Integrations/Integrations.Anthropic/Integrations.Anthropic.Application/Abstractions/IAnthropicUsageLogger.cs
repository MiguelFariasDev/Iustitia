using Advocacia.Modules.Integrations.Anthropic.Application.Models;
using Advocacia.Modules.Integrations.Anthropic.Domain.Models;

namespace Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;

/// <summary>
/// Auditoria de uso de IA (tabela ai_usage_logs — ADR-069): tokens, custo em USD, latência e
/// desfecho de cada chamada. NUNCA recebe nem grava o conteúdo das mensagens.
///
/// Falha ao gravar PROPAGA: uma chamada paga sem registro é pior que uma operação que falha e
/// é retentada.
/// </summary>
public interface IAnthropicUsageLogger
{
    /// <summary>Registra uma chamada bem-sucedida. O custo é calculado a partir de <paramref name="usage"/>.</summary>
    Task LogUsageAsync(
        Guid tenantId,
        Guid userId,
        string model,
        AnthropicUsage usage,
        long latencyMs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forma completa: permite registrar falha, finalidade, versão de prompt e a referência ao
    /// objeto de negócio — usada, por exemplo, para registrar uma resposta cobrada mas inválida.
    /// </summary>
    Task LogAsync(AnthropicUsageLogEntry entry, CancellationToken cancellationToken = default);
}
