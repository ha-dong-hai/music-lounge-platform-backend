namespace MusicLounge.Domain.Entities;

public sealed class UserFavouriteMood : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public int MoodId { get; set; }

    public User User { get; set; } = null!;
    public Mood Mood { get; set; } = null!;
}
