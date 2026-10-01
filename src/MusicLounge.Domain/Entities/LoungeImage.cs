namespace MusicLounge.Domain.Entities;

public sealed class LoungeImage : Common.BaseEntity<Guid>
{
    public Guid LoungeId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsPrimary { get; set; } = false;
    public DateTimeOffset UploadedAt { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
}
