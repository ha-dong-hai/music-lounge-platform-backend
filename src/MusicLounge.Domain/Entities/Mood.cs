namespace MusicLounge.Domain.Entities;

// AuditableEntity (CreatedAt/UpdatedAt/CreatedBy/UpdatedBy) — cùng lý do với EventCategory.
public sealed class Mood : Common.AuditableEntity<int>
{
    public string Name { get; set; } = string.Empty;

    public ICollection<LoungeShowMood> LoungeShowMoods { get; set; } = [];
    public ICollection<UserFavouriteMood> UserFavourites { get; set; } = [];
}
