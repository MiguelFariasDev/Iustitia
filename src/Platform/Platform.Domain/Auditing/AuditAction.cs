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
}
