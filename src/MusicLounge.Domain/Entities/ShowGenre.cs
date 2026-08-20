// CoreFlow: CF2 (Event Discovery)
// Junction table linking a show to its music genres — used for search filtering and AI matching.
namespace MusicLounge.Domain.Entities;

public class ShowGenre
{
    public int ShowId { get; set; }
    public int GenreId { get; set; }
}
