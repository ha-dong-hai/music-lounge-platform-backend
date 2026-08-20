namespace MusicLounge.Domain.Entities;

public sealed class Mood : Common.BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;

    public ICollection<LoungeShowMood> LoungeShowMoods { get; set; } = [];
    public ICollection<UserFavouriteMood> UserFavourites { get; set; } = [];
}
