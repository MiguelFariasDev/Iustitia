using System.Diagnostics;
using System.Diagnostics.Metrics;
using Advocacia.BuildingBlocks.Infrastructure.Observability;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure.Observability;

/// <summary>
/// Métricas das chamadas à Anthropic. Usa o Meter "Advocacia" (<see cref="MeterNames.Advocacia"/>),
/// o único que o OpenTelemetry coleta (ver CnjCaptureTelemetry). Os rótulos são modelo,
/// sucesso e código de erro — nunca tenant, usuário ou conteúdo (cardinalidade e sigilo).
/// </summary>
public sealed class AnthropicMetrics : IDisposable
{
    public const string InputTokensName = "anthropic.tokens.input";
    public const string OutputTokensName = "anthropic.tokens.output";
    public const string CostUsdName = "anthropic.cost.usd";
    public const string DurationName = "anthropic.requests.duration";

    private readonly Meter _meter = new(MeterNames.Advocacia);
    private readonly Counter<long> _inputTokens;
    private readonly Counter<long> _outputTokens;
    private readonly Counter<double> _costUsd;
    private readonly Histogram<double> _duration;

    public AnthropicMetrics()
    {
        _inputTokens = _meter.CreateCounter<long>(InputTokensName, "{token}", "Tokens de entrada enviados à Anthropic (inclui cache).");
        _outputTokens = _meter.CreateCounter<long>(OutputTokensName, "{token}", "Tokens de saída gerados pela Anthropic.");
        _costUsd = _meter.CreateCounter<double>(CostUsdName, "USD", "Custo estimado das chamadas à Anthropic, em dólares.");
        _duration = _meter.CreateHistogram<double>(DurationName, "ms", "Duração das chamadas à Anthropic, incluindo retries.");
    }

    public void Record(string model, bool success, string? errorCode, long inputTokens, long outputTokens, double costUsd, double durationMs)
    {
        var tags = new TagList
        {
            { "model", model },
            { "success", success },
            { "error_code", errorCode ?? "none" },
        };

        _inputTokens.Add(inputTokens, tags);
        _outputTokens.Add(outputTokens, tags);
        _costUsd.Add(costUsd, tags);
        _duration.Record(durationMs, tags);
    }

    public void Dispose() => _meter.Dispose();
}
