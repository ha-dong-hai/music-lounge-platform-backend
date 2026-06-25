// CoreFlow: CF2 (Event Discovery)
// Venue-defined custom matching criteria for AI recommendation.
// Each venue can define its own criteria (e.g. "Performance Language", "Has Acoustic Set?").
// Users can set preferences for these criteria which the AI uses to improve recommendations.
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class CustomCriteria : BaseEntity<int>
{
    public int LoungeId { get; set; }
    // Human-readable label e.g. "Ngôn ngữ biểu diễn"
    public string Name { get; set; } = string.Empty;
    // Machine-readable key e.g. "performance_language"
    public string Key { get; set; } = string.Empty;
    public CustomCriteriaDataType DataType { get; set; }
    // JSON: select → ["VI","EN"] | range → { min, max, step } | null for boolean/text
    public string? Options { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
