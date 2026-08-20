// CoreFlow: CF2 (Event Discovery)
// Junction table linking a show to its atmospheres — used for AI content-based recommendation.
namespace MusicLounge.Domain.Entities;

public class ShowAtmosphere
{
    public int ShowId { get; set; }
    public int AtmosphereId { get; set; }
}
