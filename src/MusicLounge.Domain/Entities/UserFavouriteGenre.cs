namespace MusicLounge.Domain.Entities;

public sealed class UserFavouriteGenre : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public int GenreId { get; set; }

    public User User { get; set; } = null!;
    public MusicGenre Genre { get; set; } = null!;
}
