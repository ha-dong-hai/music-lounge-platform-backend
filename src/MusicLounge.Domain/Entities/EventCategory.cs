namespace MusicLounge.Domain.Entities;

public sealed class EventCategory : Common.BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<LoungeShow> Shows { get; set; } = [];
}
