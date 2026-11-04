using Advocacia.Platform.Domain.Identity;
using Hangfire.Dashboard;

namespace Advocacia.Platform.Infrastructure.Jobs.HangfireConfiguration;

/// <summary>Protege o dashboard do Hangfire (/jobs) — só usuários autenticados com papel Admin.</summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole(UserRole.Admin.ToString());
    }
}
