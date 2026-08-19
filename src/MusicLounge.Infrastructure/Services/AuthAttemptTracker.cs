using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

// MaxFailedAttempts/LockoutDuration live in SecurityDetectionSettings (appsettings), not
// system_config — see that class's own header comment. Raising either is exactly what a
// credential-stuffing attacker with DB write access would want; system_config has no write API
// today so it's reachable by the same compromise this lockout exists to slow down.
internal sealed class AuthAttemptTracker : IAuthAttemptTracker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SecurityDetectionSettings _settings;

    public AuthAttemptTracker(IServiceScopeFactory scopeFactory, IOptions<SecurityDetectionSettings> settings)
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

        var user = await db.Set<User>().FindAsync([userId], ct);
        if (user is null) return;

        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= _settings.MaxFailedLoginAttempts)
        {
            user.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(_settings.LockoutDurationMinutes);
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
