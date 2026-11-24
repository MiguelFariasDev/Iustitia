namespace Advocacia.Platform.Domain.Auditing;

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
    Login,
    LoginFailed,
    Logout,
    Invited,
    Activated,
    Deactivated,
    Reviewed,
    Approved,
    Rejected,
    Corrected,
    Viewed,
    Uploaded,
    Downloaded,

    /// <summary>Dados pessoais eliminados de forma irreversível a pedido do titular (LGPD).</summary>
    Anonymized,

    /// <summary>Cópia dos dados pessoais gerada para o titular (LGPD).</summary>
    Exported,

    /// <summary>Lançamento financeiro quitado.</summary>
    Paid,
}
