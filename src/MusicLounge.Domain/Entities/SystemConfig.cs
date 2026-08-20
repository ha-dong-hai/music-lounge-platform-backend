using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// D9: ALL business parameters live here — never hardcode in code
public sealed class SystemConfig : Common.BaseEntity<int>
{
    public string ConfigKey { get; set; } = string.Empty;   // UNIQUE
    public string ConfigValue { get; set; } = string.Empty;
    public ConfigDataType DataType { get; set; }
    public string? Description { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User? UpdatedByUser { get; set; }
    public ICollection<SystemConfigHistory> History { get; set; } = [];
}
