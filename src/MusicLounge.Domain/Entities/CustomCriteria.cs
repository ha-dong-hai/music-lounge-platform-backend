using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// AI custom: each venue defines its own criteria for recommendation
public sealed class CustomCriteria : Common.BaseEntity<int>
{
    public int LoungeId { get; set; }
    public string Name { get; set; } = string.Empty;       // "Ngôn ngữ biểu diễn"
    public string Key { get; set; } = string.Empty;        // machine-readable: "performance_language"
    public CustomCriteriaDataType DataType { get; set; }
    public string? Options { get; set; }    // JSON: select→["VI","EN"] / range→{min,max,step}
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
    public ICollection<EventCustomValue> EventValues { get; set; } = [];
    public ICollection<UserCustomPreference> UserPreferences { get; set; } = [];
}
