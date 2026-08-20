using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class MusicShowWishlist : BaseEntity<int>
{
    public int UserId { get; set; }
    public int ShowId { get; set; }
    public DateTime SavedAt { get; set; }
}
