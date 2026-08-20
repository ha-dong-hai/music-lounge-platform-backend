using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class PerformerSocialLink : Common.BaseEntity<int>
{
    public int PerformerId { get; set; }
    public SocialPlatform Platform { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? DisplayName { get; set; }

    public Performer Performer { get; set; } = null!;
}
