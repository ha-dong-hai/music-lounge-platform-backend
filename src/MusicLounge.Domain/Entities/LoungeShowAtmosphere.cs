namespace MusicLounge.Domain.Entities;

public sealed class LoungeShowAtmosphere : Common.BaseEntity<Guid>
{
    public Guid LoungeShowId { get; set; }
    public Guid AtmosphereId { get; set; }

    public LoungeShow LoungeShow { get; set; } = null!;
    public VenueAtmosphere Atmosphere { get; set; } = null!;
}
