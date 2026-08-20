namespace MusicLounge.Domain.Entities;

// 1 MusicLounge có thể có nhiều FnbMenu (VD: "Menu Sáng", "Menu Tối", "Menu Cuối tuần")
public sealed class FnbMenu : Common.AuditableEntity<int>
{
    public int LoungeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;

    public MusicLounge Lounge { get; set; } = null!;
    public ICollection<FnbMenuItem> Items { get; set; } = [];
}
