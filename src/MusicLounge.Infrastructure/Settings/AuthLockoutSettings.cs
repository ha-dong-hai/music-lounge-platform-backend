namespace MusicLounge.Infrastructure.Settings;

// Security-sensitive thresholds live in appsettings, not system_config — same boundary as JwtSettings
// (system_config has no write-audit trail suitable for tuning values that control brute-force
// resistance; see ISystemConfigService's own D9 comment for the general "business parameters belong
// in system_config" rule this deliberately sits outside of).
public sealed class AuthLockoutSettings
{
    public int MaxFailedAttempts { get; init; } = 5;
    public int LockoutDurationMinutes { get; init; } = 15;
}
