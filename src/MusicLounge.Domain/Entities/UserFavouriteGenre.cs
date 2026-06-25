// CoreFlow: CF2 (Event Discovery)
// Records a user's preferred music genres — used as input for AI content-based recommendation.
// CreatedAt is used as a recency signal: more recently added preferences carry more weight.
namespace MusicLounge.Domain.Entities;

public class UserFavouriteGenre
{
    public int UserId { get; set; }
    public int GenreId { get; set; }
    public DateTime CreatedAt { get; set; }
}
