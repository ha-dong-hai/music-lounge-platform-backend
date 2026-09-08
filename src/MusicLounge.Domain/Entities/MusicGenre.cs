namespace MusicLounge.Domain.Entities;

// AuditableEntity (CreatedAt/UpdatedAt/CreatedBy/UpdatedBy) — cùng lý do với EventCategory.
public sealed class MusicGenre : Common.AuditableEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }

    public ICollection<LoungeShowGenre> LoungeShowGenres { get; set; } = [];
    public ICollection<PerformerGenre> PerformerGenres { get; set; } = [];
    public ICollection<UserFavouriteGenre> UserFavourites { get; set; } = [];
}
