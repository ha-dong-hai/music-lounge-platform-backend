namespace MusicLounge.Domain.Entities;

public sealed class UserFavouriteMood : Common.BaseEntity<Guid>
{
    public Guid UserId { get; set; }
    public Guid MoodId { get; set; }

    public User User { get; set; } = null!;
    public Mood Mood { get; set; } = null!;
}
