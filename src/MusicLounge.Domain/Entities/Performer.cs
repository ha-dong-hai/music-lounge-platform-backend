using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class Performer : Common.AuditableEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public PerformerType Type { get; set; } = PerformerType.Solo;
    public int? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }
    public ICollection<PerformerGenre> Genres { get; set; } = [];
    public ICollection<Performance> Performances { get; set; } = [];
}
