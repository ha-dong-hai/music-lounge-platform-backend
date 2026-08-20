using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// Every panorama-stitch attempt for a venue's tour — a log, not just the current scenes. Mirrors
// AiPosterGeneration's role: (a) tour_stitch_max_attempts_per_lounge (system_config) is enforced
// against EVERY attempt here (success + failure) as an anti-abuse rate limit — unlike the AI
// poster vendor calls, a stitch runs on OUR OWN server's CPU, so an unbounded retry loop is a
// direct cost/DoS vector, not just a wasted vendor bill; and (b) it's an auditable record of why a
// given stitch failed, since the resulting VenueTourScene (on success) doesn't carry that context.
public sealed class VenueTourStitchAttempt : Common.BaseEntity<int>
{
    public int LoungeId { get; set; }
    public VenueTourStitchStatus Status { get; set; }
    public int? ResultSceneId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
    public VenueTourScene? ResultScene { get; set; }
}
