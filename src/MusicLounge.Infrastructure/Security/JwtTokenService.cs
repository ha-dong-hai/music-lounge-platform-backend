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
}
