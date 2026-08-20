using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderUserId, out var userIdValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userIdValues.ToString()),
            new(ClaimTypes.Role,
                Request.Headers.TryGetValue(HeaderRole, out var role) ? role.ToString() : "Audience")
        };

        if (Request.Headers.TryGetValue(HeaderLoungeId, out var loungeId))
            claims.Add(new Claim("lounge_id", loungeId.ToString()));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
