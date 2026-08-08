using Microsoft.AspNetCore.Mvc.Filters;

namespace MusicLounge.Api.Filters;

/// <summary>
/// Global safety net for the "page"/"pageSize"/"limit" query parameters used across list and
/// top-N endpoints. None of them define an upper bound — verified during the
/// master-backend-techlead review — so `?pageSize=999999999` (or `?limit=999999999` on
/// suggestions/trending/recommendations, all AllowAnonymous-or-any-authenticated-user endpoints)
/// was a valid request that could pull an entire table into memory in one query (OWASP
/// A02-adjacent resource-exhaustion risk), and `?page=0`/negative values would throw an
/// unhandled ArgumentOutOfRangeException from LINQ's Skip/Take (surfacing as a raw 500 instead of
/// a clean, bounded response). Clamping once here at the framework boundary closes every existing
/// call site at once, and any future paginated/top-N endpoint inherits the same protection
/// automatically — the alternative was touching each individual controller action with no single
/// choke point to unify them through.
/// </summary>
internal sealed class ClampPaginationActionFilter : IActionFilter
{
    private const int MaxPageSize = 100;
    private const int MaxLimit = 100;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("pageSize", out var sizeValue) && sizeValue is int pageSize)
            context.ActionArguments["pageSize"] = Math.Clamp(pageSize, 1, MaxPageSize);

        if (context.ActionArguments.TryGetValue("page", out var pageValue) && pageValue is int page && page < 1)
            context.ActionArguments["page"] = 1;

        if (context.ActionArguments.TryGetValue("limit", out var limitValue) && limitValue is int limit)
            context.ActionArguments["limit"] = Math.Clamp(limit, 1, MaxLimit);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
