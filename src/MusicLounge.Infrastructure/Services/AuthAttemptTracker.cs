using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Services;

internal sealed class AuthAttemptTracker : IAuthAttemptTracker
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;

    public AuthAttemptTracker(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<TimeSpan?> GetLockoutRemainingAsync(int userId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lockedUntil = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.LockedUntil)
            .FirstOrDefaultAsync(ct);

        if (lockedUntil is null) return null;
        var remaining = lockedUntil.Value - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    public async Task RecordFailureAsync(int userId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Set<User>().FindAsync([userId], ct);
        if (user is null) return;

        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= MaxFailedAttempts)
        {
            user.LockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
            user.FailedLoginAttempts = 0;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ResetAsync(int userId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Set<User>().FindAsync([userId], ct);
        if (user is null || (user.FailedLoginAttempts == 0 && user.LockedUntil is null)) return;

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await db.SaveChangesAsync(ct);
    }
}
