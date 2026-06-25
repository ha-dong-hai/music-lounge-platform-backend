// CoreFlow: CF1 (Event Management), CF2 (Event Discovery)
// Photos of the venue displayed on the lounge detail page.
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class LoungeImage : BaseEntity<int>
{
    public int LoungeId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; } = 0;
    // Only one image per lounge can be primary — enforced via partial unique index in DB
    public bool IsPrimary { get; set; } = false;
    public DateTime UploadedAt { get; set; }
}
