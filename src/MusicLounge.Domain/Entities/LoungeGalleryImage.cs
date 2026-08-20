namespace MusicLounge.Domain.Entities;

// Multiple showcase photos for a venue's detail page — distinct from PrimaryImageUrl (the single
// "hero" thumbnail used in lounge listings) and from VenueTourScene (360° panoramas used for
// spatial navigation, gated by subscription). Free for every Owner, same as PrimaryImageUrl.
public sealed class LoungeGalleryImage : Common.BaseEntity<int>
{
    public int LoungeId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int OrderIndex { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
}
