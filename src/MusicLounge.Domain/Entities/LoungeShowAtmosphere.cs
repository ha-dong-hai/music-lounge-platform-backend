namespace MusicLounge.Domain.Entities;

public sealed class LoungeShowAtmosphere : Common.BaseEntity<int>
{
    public int LoungeShowId { get; set; }
    public int AtmosphereId { get; set; }

    public LoungeShow LoungeShow { get; set; } = null!;
    public VenueAtmosphere Atmosphere { get; set; } = null!;
}
