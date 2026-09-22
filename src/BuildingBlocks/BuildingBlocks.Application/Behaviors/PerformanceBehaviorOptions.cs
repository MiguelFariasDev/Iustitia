namespace Advocacia.BuildingBlocks.Application.Behaviors;

/// <summary>Seção de configuração "Performance" (appsettings) — ver PerformanceBehavior.</summary>
public sealed class PerformanceBehaviorOptions
{
    public const string SectionName = "Performance";

    /// <summary>Acima deste limite, PerformanceBehavior loga um warning. Padrão: 500ms.</summary>
    public int ThresholdMilliseconds { get; set; } = 500;
}
