// CoreFlow: CF2 (Event Discovery)
// A user's preference value for a venue's custom criteria — used in AI personalized scoring.
// Weight represents how important this criterion is to the user (0 = don't care, 1 = very important).
// Weight is updated using EMA: weight_new = 0.3 × new_signal + 0.7 × old_weight.
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class UserCustomPreference : BaseEntity<int>
{
    public int UserId { get; set; }
    public int CriteriaId { get; set; }
    public string Value { get; set; } = string.Empty;
    public UserPreferenceSource Source { get; set; }
    // Importance weight: 0.0 = not important, 1.0 = critical preference
    public decimal Weight { get; set; } = 0.5m;
    public DateTime UpdatedAt { get; set; }
}
