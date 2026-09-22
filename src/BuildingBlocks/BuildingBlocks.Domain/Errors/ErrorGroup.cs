namespace Advocacia.BuildingBlocks.Domain.Errors;

/// <summary>
/// Agrupamento de <see cref="ErrorCode"/> para classificação, dashboards e relatórios —
/// ver docs/errors/groups.md para a lista completa (incluindo grupos futuros ainda não
/// implementados, reservados por faixa numérica em <see cref="ErrorCode"/>).
/// </summary>
public enum ErrorGroup
{
    Common,
    Validation,
    Auth,
    Authorization,
    Tenant,
    User,
    Audit,
    Settings,
    FeatureFlags,
    Backup,
    Integration,
    Internal,
}
