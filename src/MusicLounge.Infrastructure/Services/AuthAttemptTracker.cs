using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

internal sealed class AuthAttemptTracker : IAuthAttemptTracker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuthLockoutSettings _settings;

    public AuthAttemptTracker(IServiceScopeFactory scopeFactory, IOptions<AuthLockoutSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
    }

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

        // Single atomic UPDATE rather than read-modify-write. The old version loaded the row,
        // incremented in memory and saved: N concurrent wrong-password requests all read the same
        // starting value and all wrote start+1, so the counter under-counted and the account took
        // materially more than MaxFailedAttempts failures to lock — weakening the brute-force
        // defence exactly when it is under attack. Both SET expressions below are evaluated by the
        // database against the row's pre-update values, so they agree on the same threshold test.
        var max = _settings.MaxFailedAttempts;
        var lockedUntil = DateTimeOffset.UtcNow.AddMinutes(_settings.LockoutDurationMinutes);

        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    u => u.LockedUntil,
                    u => u.FailedLoginAttempts + 1 >= max ? lockedUntil : u.LockedUntil)
                .SetProperty(
                    u => u.FailedLoginAttempts,
                    u => u.FailedLoginAttempts + 1 >= max ? 0 : u.FailedLoginAttempts + 1),
                ct);
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
