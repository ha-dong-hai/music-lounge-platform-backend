namespace MusicLounge.Domain.Entities;

// AuditableEntity (CreatedAt/UpdatedAt/CreatedBy/UpdatedBy) — cùng lý do với EventCategory.
public sealed class VenueAtmosphere : Common.AuditableEntity<int>
{
    public string Name { get; set; } = string.Empty;

    public ICollection<LoungeShowAtmosphere> LoungeShowAtmospheres { get; set; } = [];
    public ICollection<UserFavouriteAtmosphere> UserFavourites { get; set; } = [];
}
