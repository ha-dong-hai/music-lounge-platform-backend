namespace MusicLounge.Domain.Entities;

public sealed class LoungeShowGenre : Common.BaseEntity<int>
{
    public int LoungeShowId { get; set; }
    public int GenreId { get; set; }

    public LoungeShow LoungeShow { get; set; } = null!;
    public MusicGenre Genre { get; set; } = null!;
}
