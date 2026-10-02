namespace MusicLounge.Domain.Entities;

public sealed class UserFavouriteGenre : Common.BaseEntity<Guid>
{
    public Guid UserId { get; set; }
    public Guid GenreId { get; set; }

    public User User { get; set; } = null!;
    public MusicGenre Genre { get; set; } = null!;
}
