// CoreFlow: CF2 (Event Discovery)
// Junction table linking a performer to their music genres — used in AI content-based matching.
namespace MusicLounge.Domain.Entities;

public class PerformerGenre
{
    public int PerformerId { get; set; }
    public int GenreId { get; set; }
}
