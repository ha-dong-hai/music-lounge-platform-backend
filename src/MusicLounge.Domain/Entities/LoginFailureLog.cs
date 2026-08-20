namespace MusicLounge.Domain.Entities;

// Every failed login attempt (known or unknown email) — distinct from IAuthAttemptTracker's
// per-account lockout counter on User itself. That counter only ever sees ONE account at a time,
// so it can't detect the credential-stuffing pattern this log is for: the SAME source IP failing
// across MANY DIFFERENT accounts. Short-lived by design (LoginSpikeDetectionJob prunes rows older
// than its own detection window) — this is a rolling security signal, not a permanent audit trail.
public sealed class LoginFailureLog : Common.BaseEntity<int>
{
    public string Email { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
