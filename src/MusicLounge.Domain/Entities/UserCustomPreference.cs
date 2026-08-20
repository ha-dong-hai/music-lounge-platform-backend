using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class UserCustomPreference : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public int CriteriaId { get; set; }
    public string Value { get; set; } = string.Empty;  // JSON value
    public CustomPreferenceSource Source { get; set; }
    // EMA: weight_new = 0.3 × new_signal + 0.7 × old_weight
    public decimal Weight { get; set; } = 0.5m;        // 0=don't care, 1=very important
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public CustomCriteria Criteria { get; set; } = null!;
}
