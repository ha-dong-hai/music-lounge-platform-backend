using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class Atmosphere : BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;
}
