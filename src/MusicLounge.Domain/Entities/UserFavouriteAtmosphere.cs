namespace MusicLounge.Domain.Entities;

public sealed class UserFavouriteAtmosphere : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public int AtmosphereId { get; set; }

    public User User { get; set; } = null!;
    public VenueAtmosphere Atmosphere { get; set; } = null!;
}
