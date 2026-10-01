namespace MusicLounge.Domain.Entities;

public sealed class UserFavouriteAtmosphere : Common.BaseEntity<Guid>
{
    public Guid UserId { get; set; }
    public Guid AtmosphereId { get; set; }

    public User User { get; set; } = null!;
    public VenueAtmosphere Atmosphere { get; set; } = null!;
}
