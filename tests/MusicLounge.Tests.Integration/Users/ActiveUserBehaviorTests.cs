using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Users;

/// <summary>
/// master-backend-techlead review of Deactivate*AccountCommandHandler — both just flip
/// User.IsActive with no server-side session/token revocation. IsActive was previously checked
/// only at Login/GoogleLogin time, so a token issued before deactivation stayed fully valid for
/// its whole remaining lifetime. ActiveUserBehavior re-checks IsActive on every authenticated
/// request to close that window.
/// </summary>
[Collection("Integration")]
public sealed class ActiveUserBehaviorTests
{
    private readonly ApiFactory _factory;

    public ActiveUserBehaviorTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DeactivatedUser_SubsequentAuthenticatedRequest_Returns401()
    {
        // AudienceId's still-"valid" test identity keeps working until we deactivate it below —
        // this mirrors a real JWT that stays cryptographically valid after the account is banned.
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var before = await client.GetAsync("/api/v1/me");
        before.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == SeedHelper.AudienceId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        try
        {
            var after = await client.GetAsync("/api/v1/me");
            after.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "the account was deactivated after the token was issued — IsActive must be " +
                "re-checked per-request, not just at login");
        }
        finally
        {
            // Restore seeded state so other tests sharing this collection's DB aren't affected.
            using var cleanupScope = _factory.Services.CreateScope();
            var db = cleanupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == SeedHelper.AudienceId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }
    }
}
