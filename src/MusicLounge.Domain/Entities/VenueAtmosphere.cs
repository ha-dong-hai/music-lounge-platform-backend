namespace MusicLounge.Domain.Entities;

public sealed class VenueAtmosphere : Common.BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;

    public ICollection<LoungeShowAtmosphere> LoungeShowAtmospheres { get; set; } = [];
    public ICollection<UserFavouriteAtmosphere> UserFavourites { get; set; } = [];
}
