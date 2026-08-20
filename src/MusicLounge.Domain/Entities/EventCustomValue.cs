namespace MusicLounge.Domain.Entities;

public sealed class EventCustomValue : Common.BaseEntity<int>
{
    public int ShowId { get; set; }
    public int CriteriaId { get; set; }
    public string Value { get; set; } = string.Empty;  // JSON value

    public LoungeShow Show { get; set; } = null!;
    public CustomCriteria Criteria { get; set; } = null!;
}
