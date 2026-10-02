namespace MusicLounge.Domain.Entities;

public sealed class PerformerGenre : Common.BaseEntity<Guid>
{
    public Guid PerformerId { get; set; }
    public Guid GenreId { get; set; }

    public Performer Performer { get; set; } = null!;
    public MusicGenre Genre { get; set; } = null!;
}
