using Advocacia.Modules.Integrations.Anthropic.Domain.Models;

namespace Advocacia.Modules.Integrations.Anthropic.Application.Abstractions;

/// <summary>Converte tokens em custo (USD) pela tabela de preços configurada.</summary>
public interface IAiCostCalculator
{
    /// <summary>
    /// Custo em USD da chamada. Modelo sem preço configurado devolve 0 e registra aviso: o
    /// log de uso continua tendo os tokens, que são a fonte de verdade para reconciliar.
    /// </summary>
    decimal Calculate(string model, AnthropicUsage usage);
}
