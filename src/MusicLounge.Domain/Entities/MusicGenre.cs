namespace MusicLounge.Domain.Entities;

public sealed class MusicGenre : Common.BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }

    public ICollection<LoungeShowGenre> LoungeShowGenres { get; set; } = [];
    public ICollection<PerformerGenre> PerformerGenres { get; set; } = [];
    public ICollection<UserFavouriteGenre> UserFavourites { get; set; } = [];
}
