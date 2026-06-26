using System;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);

    DateTime GetTokenExpiryUtc();
}
