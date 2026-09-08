using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class Genre : BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;
}
