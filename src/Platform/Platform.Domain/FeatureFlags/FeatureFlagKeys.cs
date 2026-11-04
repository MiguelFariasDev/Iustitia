namespace Advocacia.Platform.Domain.FeatureFlags;

/// <summary>
/// Chaves conhecidas de feature flags por tenant (ver ADR-038) — cada uma corresponde a um
/// módulo/funcionalidade liberado gradualmente conforme o roadmap de fases do CLAUDE.md.
/// Usar estas constantes em vez de strings soltas ao chamar IFeatureFlagService.
/// </summary>
public static class FeatureFlagKeys
{
    public const string LegalModule = "legal.module";
    public const string WorkflowModule = "workflow.module";
    public const string AiClassification = "ai.classification";
    public const string AthenaAssistant = "athena.assistant";
    public const string PublicApi = "public.api";
    public const string WhatsAppNotifications = "whatsapp.notifications";
    public const string GoogleCalendarSync = "google.calendar.sync";
    public const string KanbanBoard = "kanban.board";
    public const string FinancialModule = "financial.module";
}
