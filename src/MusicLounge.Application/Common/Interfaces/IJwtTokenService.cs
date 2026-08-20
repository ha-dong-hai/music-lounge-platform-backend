using System.Security.Claims;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) GenerateToken(User user, int? loungeId);

    (string Token, DateTimeOffset ExpiresAt) GenerateRefreshToken(User user);

    /// <summary>
    /// Validates signature/issuer/audience/expiry and that the token carries token_type=refresh
    /// (so an access token can't be replayed here). Returns null if any check fails — caller still
    /// must compare the "sec_stamp" claim against the user's current SecurityStamp to catch a
    /// refresh token issued before a logout/security-stamp rotation.
    /// </summary>
    ClaimsPrincipal? ValidateRefreshToken(string token);
}
