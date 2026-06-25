// CoreFlow: CF2 (Event Discovery)
// Records a user's preferred atmospheres — used as input for AI content-based recommendation.
namespace MusicLounge.Domain.Entities;

public class UserFavouriteAtmosphere
{
    public int UserId { get; set; }
    public int AtmosphereId { get; set; }
    public DateTime CreatedAt { get; set; }
}
