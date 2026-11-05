namespace Advocacia.Modules.Workflow.Application;

/// <summary>Configuração do módulo (<c>Workflow</c>).</summary>
public sealed class WorkflowOptions
{
    public const string SectionName = "Workflow";

    /// <summary>
    /// Fuso em que o escritório conta "hoje". Padrão <c>America/Sao_Paulo</c>. É uma opção — e não uma
    /// constante — porque o produto não tem ainda o fuso por escritório; quando tiver, o
    /// <see cref="BusinessCalendar"/> passa a lê-lo do tenant.
    /// </summary>
    public string TimeZoneId { get; init; } = "America/Sao_Paulo";
}
