using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Security;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.Auth;

/// <summary>
/// JwtTokenService issues the only bearer credential this app has — no refresh tokens, no
/// revocation list. Program.cs's OnTokenValidated revokes access on password reset / deactivation
/// by comparing the "sec_stamp" claim embedded here against User.SecurityStamp in the DB, so a
/// typo in the claim name or value here would silently defeat that check. Exercised as a plain
/// unit test (no WebApplicationFactory/HTTP) because ApiFactory replaces real JWT bearer auth with
/// TestAuthHandler for every other test in this project — this is the only place the claim side of
/// that contract gets verified.
/// </summary>
public sealed class JwtTokenServiceTests
{
    private static JwtTokenService CreateService() => new(Options.Create(new JwtSettings
    {
        Secret = new string('a', 32),
        Issuer = "test-issuer",
        Audience = "test-audience",
        AccessTokenExpiryMinutes = 60
    }));

    [Fact]
    public void GenerateToken_EmbedsSecurityStampMatchingUser()
    {
        var user = new User
        {
            Id = 42,
            Email = "stamp@test.com",
            Role = UserRole.Audience,
            SecurityStamp = Guid.NewGuid()
        };

        var (token, _) = CreateService().GenerateToken(user, null);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var stampClaim = jwt.Claims.SingleOrDefault(c => c.Type == "sec_stamp");
        stampClaim.Should().NotBeNull();
        stampClaim!.Value.Should().Be(user.SecurityStamp.ToString());
    }

    [Fact]
    public void GenerateToken_DifferentUsers_ProduceDifferentSecurityStamps()
    {
        var userA = new User { Id = 1, Email = "a@test.com", Role = UserRole.Audience, SecurityStamp = Guid.NewGuid() };
        var userB = new User { Id = 2, Email = "b@test.com", Role = UserRole.Audience, SecurityStamp = Guid.NewGuid() };
        var service = CreateService();

        var (tokenA, _) = service.GenerateToken(userA, null);
        var (tokenB, _) = service.GenerateToken(userB, null);

        var handler = new JwtSecurityTokenHandler();
        var stampA = handler.ReadJwtToken(tokenA).Claims.Single(c => c.Type == "sec_stamp").Value;
        var stampB = handler.ReadJwtToken(tokenB).Claims.Single(c => c.Type == "sec_stamp").Value;
        stampA.Should().NotBe(stampB);
    }
}
