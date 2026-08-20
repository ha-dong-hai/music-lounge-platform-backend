// CoreFlow: CF2 (Event Discovery)
// Junction table linking a show to its moods — used for AI content-based recommendation.
namespace MusicLounge.Domain.Entities;

public class ShowMood
{
    public int ShowId { get; set; }
    public int MoodId { get; set; }
}
