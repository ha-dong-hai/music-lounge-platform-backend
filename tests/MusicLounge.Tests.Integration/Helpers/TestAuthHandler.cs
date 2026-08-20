using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Tests.Integration.Helpers;

/// <summary>
/// Bypasses JWT validation in integration tests.
/// Reads X-Test-User-Id, X-Test-User-Role, X-Test-Lounge-Id from request headers.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string HeaderUserId = "X-Test-User-Id";
    public const string HeaderRole = "X-Test-User-Role";
    public const string HeaderLoungeId = "X-Test-Lounge-Id";

    private readonly ApplicationDbContext _db;

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApplicationDbContext db)
        : base(options, logger, encoder)
        => _db = db;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderUserId, out var userIdValues))
            return AuthenticateResult.NoResult();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userIdValues.ToString()),
            new(ClaimTypes.Role,
                Request.Headers.TryGetValue(HeaderRole, out var role) ? role.ToString() : "Audience")
        };

        if (Request.Headers.TryGetValue(HeaderLoungeId, out var loungeId))
            claims.Add(new Claim("lounge_id", loungeId.ToString()));

        // ActiveUserBehavior compares this against the real User.SecurityStamp — a stale/missing
        // claim here would 401 every authenticated test request, not just ones that actually care
        // about logout/token revocation, so it has to reflect the DB's current value, not a fixed
        // stub.
        if (int.TryParse(userIdValues.ToString(), out var userId))
        {
            var securityStamp = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => (Guid?)u.SecurityStamp)
                .FirstOrDefaultAsync();
            if (securityStamp is { } stamp)
                claims.Add(new Claim("sec_stamp", stamp.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
