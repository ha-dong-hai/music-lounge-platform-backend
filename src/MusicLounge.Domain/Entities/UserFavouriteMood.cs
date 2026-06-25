// CoreFlow: CF2 (Event Discovery)
// Records a user's preferred moods — used as input for AI content-based recommendation.
namespace MusicLounge.Domain.Entities;

public class UserFavouriteMood
{
    public int UserId { get; set; }
    public int MoodId { get; set; }
    public DateTime CreatedAt { get; set; }
}
