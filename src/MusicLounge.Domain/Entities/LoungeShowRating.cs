namespace MusicLounge.Domain.Entities;

// AuditableEntity — UpdatedBy/UpdatedAt is genuinely new info RemoveRatingCommandHandler never
// recorded before: which Admin removed this rating and when (only IsRemoved/RemovedReason existed,
// no actor/timestamp). CreatedBy is redundant with UserId on the one creation path, but harmless.
public sealed class LoungeShowRating : Common.AuditableEntity<Guid>
{
    public Guid? UserId { get; set; }
    public Guid LoungeShowId { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public bool IsRemoved { get; set; } = false;
    public string? RemovedReason { get; set; }

    public User? User { get; set; }
    public LoungeShow LoungeShow { get; set; } = null!;
}
