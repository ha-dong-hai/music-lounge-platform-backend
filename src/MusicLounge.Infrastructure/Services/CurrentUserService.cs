using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Infrastructure.Services;

internal sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated == true;

    // Every real caller sits behind [Authorize], so the NameIdentifier claim is always present —
    // but silently defaulting to 0 here (OWASP A10: failing open instead of closed) would mean
    // any future caller that forgets to check IsAuthenticated first attributes its action to a
    // "User 0" that doesn't exist, instead of the operation visibly failing. Throw instead.
    public int UserId =>
        int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedException("Không xác định được người dùng hiện tại.");

    public string Role =>
        User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public int? LoungeId
    {
        get
        {
            var claim = User?.FindFirstValue("lounge_id");
            return claim is not null ? int.Parse(claim) : null;
        }
    }
}
