using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class Follow : BaseEntity<int>
{
    public int UserId { get; set; }
    public int LoungeId { get; set; }
    // Used as recency signal in AI recommendation scoring
    public DateTime CreatedAt { get; set; }
}
