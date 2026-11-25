using System.Diagnostics.Metrics;
using Advocacia.BuildingBlocks.Infrastructure.Observability;

namespace Advocacia.Modules.Legal.Infrastructure.Observability;

/// <summary>
/// Métrica de qual fonte atendeu a consulta de processo no tribunal (ver ADR-075). Usa o Meter
/// "Advocacia", já coletado pelo OpenTelemetry (ver <see cref="CnjCaptureTelemetry"/>).
///
/// Tags só com valores de baixa cardinalidade: fonte, tribunal e motivo. O NÚMERO DO PROCESSO
/// nunca vira tag nem atributo — identificaria o caso e explodiria a cardinalidade.
/// </summary>
public sealed class ConsultaTribunalTelemetry : IDisposable
{
    public const string ContadorFonte = "consulta_tribunal.fonte";

    private readonly Meter _meter = new(MeterNames.Advocacia);
    private readonly Counter<long> _fonte;

    public ConsultaTribunalTelemetry()
    {
        _fonte = _meter.CreateCounter<long>(
            ContadorFonte, description: "Consultas de processo no tribunal, por fonte que respondeu e motivo do fallback.");
    }

    /// <param name="fonte">"juscraper" ou "datajud".</param>
    /// <param name="tribunal">Sigla do tribunal (ex.: TJSP).</param>
    /// <param name="motivoFallback">Código do juscraper que levou ao DataJud; nulo quando não houve fallback.</param>
    public void Registrar(string fonte, string? tribunal, string? motivoFallback) =>
        _fonte.Add(
            1,
            new KeyValuePair<string, object?>("fonte", fonte),
            new KeyValuePair<string, object?>("tribunal", tribunal ?? "desconhecido"),
            new KeyValuePair<string, object?>("motivo_fallback", motivoFallback ?? "nenhum"));

    public void Dispose() => _meter.Dispose();
}
