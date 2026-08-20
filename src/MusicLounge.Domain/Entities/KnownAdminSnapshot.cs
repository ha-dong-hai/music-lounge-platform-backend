namespace MusicLounge.Domain.Entities;

// Baseline for AdminRoleDriftDetectionJob — "is this User.Id a Role=Admin account we already knew
// about" independent of HOW it became Admin. There is currently no API path to promote a user to
// Admin at all, so every admin today only exists via a direct database edit; this snapshot is what
// lets the job tell "an admin that was already there" apart from "an admin that just appeared".
public sealed class KnownAdminSnapshot : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public DateTimeOffset FirstDetectedAt { get; set; }
}
