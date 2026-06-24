using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class Mood : BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;
}
