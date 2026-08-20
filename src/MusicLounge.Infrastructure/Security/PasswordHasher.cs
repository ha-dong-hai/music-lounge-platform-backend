using Microsoft.AspNetCore.Identity;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Security;

internal sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _identityHasher = new();

    public string Hash(string password) => _identityHasher.HashPassword(null!, password);

    public bool Verify(string hash, string password) =>
        _identityHasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
}
