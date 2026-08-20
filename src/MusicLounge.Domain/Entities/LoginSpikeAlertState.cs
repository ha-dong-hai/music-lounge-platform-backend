namespace MusicLounge.Domain.Entities;

// De-dup state for LoginSpikeDetectionJob — without this, an ongoing attack would re-alert every
// Admin on every job run (every few minutes) for as long as it continues, which trains Admins to
// ignore the alert rather than act on it.
public sealed class LoginSpikeAlertState : Common.BaseEntity<int>
{
    public string IpAddress { get; set; } = string.Empty;
    public DateTimeOffset LastAlertedAt { get; set; }
}
