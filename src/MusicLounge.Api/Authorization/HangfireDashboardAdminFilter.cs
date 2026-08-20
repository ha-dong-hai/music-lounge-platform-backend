using Hangfire.Dashboard;
using MusicLounge.Application.Common.Constants;

namespace MusicLounge.Api.Authorization;

/// <summary>
/// The Hangfire dashboard was previously configured (appsettings.json's DashboardPath/WorkerCount)
/// but never actually mounted anywhere — a production-hardening audit finding, since that left no
/// visibility into any of the 20 background jobs' health at all. Mounting it unauthenticated would
/// trade one gap for a worse one (job internals, including payloads, exposed to anyone who finds the
/// URL) — this filter requires an authenticated request bearing the standard JWT with Role=Admin,
/// same authentication the rest of the API already uses, rather than inventing a separate credential.
/// </summary>
internal sealed class HangfireDashboardAdminFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole(Roles.Admin);
    }
}
