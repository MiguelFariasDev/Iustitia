using System.Diagnostics;
using Advocacia.BuildingBlocks.Infrastructure.Observability;

namespace Advocacia.Modules.Integrations.Anthropic.Infrastructure.Observability;

/// <summary>
/// ActivitySource "Advocacia.Anthropic" (registrada em OpenTelemetryConfiguration). Os spans
/// carregam SÓ metadados — modelo, tokens, custo, desfecho. Nunca o texto de prompt ou resposta.
/// </summary>
public static class AnthropicActivitySource
{
    public const string Name = ActivitySourceNames.Anthropic;

    public const string SendMessageSpanName = "anthropic.send_message";

    public static readonly ActivitySource Source = new(Name);
}
