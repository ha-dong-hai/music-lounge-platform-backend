using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) GenerateToken(User user, int? loungeId);
}
