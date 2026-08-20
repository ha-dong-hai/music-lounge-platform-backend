namespace MusicLounge.Domain.Entities;

public sealed class PerformerGenre : Common.BaseEntity<int>
{
    public int PerformerId { get; set; }
    public int GenreId { get; set; }

    public Performer Performer { get; set; } = null!;
    public MusicGenre Genre { get; set; } = null!;
}
