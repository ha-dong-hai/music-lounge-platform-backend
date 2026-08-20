using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Security;

internal sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public (string Token, DateTimeOffset ExpiresAt) GenerateToken(User user, int? loungeId)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(ClaimTypes.Email, user.Email),
            new("sec_stamp", user.SecurityStamp.ToString())
        };

        if (loungeId.HasValue)
            claims.Add(new Claim("lounge_id", loungeId.Value.ToString()));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return (tokenString, expiresAt);
    }

    public (string Token, DateTimeOffset ExpiresAt) GenerateRefreshToken(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_settings.RefreshTokenExpiryDays);

        // Deliberately minimal claim set — a refresh token's only job is proving "this user, this
        // security-stamp version, wants a new access token", not carrying role/email/lounge (those
        // are re-derived fresh from the DB on every refresh, same as at Login).
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sec_stamp", user.SecurityStamp.ToString()),
            new("token_type", "refresh")
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return (tokenString, expiresAt);
    }

    public ClaimsPrincipal? ValidateRefreshToken(string token)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out var validatedToken);

            // Belt-and-suspenders on top of ValidateIssuerSigningKey: reject anything not signed
            // with the exact algorithm we issue (blocks the classic "alg: none" downgrade attack).
            if (validatedToken is not JwtSecurityToken jwt ||
                !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
                return null;

            if (principal.FindFirstValue("token_type") != "refresh")
                return null;

            return principal;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException)
        {
            // ArgumentException/FormatException cover structurally-malformed input (e.g. a plain
            // string that isn't even 3 base64 segments) — JwtSecurityTokenHandler throws these
            // before it gets far enough to raise a SecurityTokenException, but it's still just
            // "not a valid token" from this method's point of view, not a real error.
            return null;
        }
    }
}
