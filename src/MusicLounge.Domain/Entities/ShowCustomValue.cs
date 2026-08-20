// CoreFlow: CF2 (Event Discovery)
// The value of a custom criteria for a specific show — set by Owner when creating the show.
// e.g. CustomCriteria "Performance Language" → value "Tiếng Việt"
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class ShowCustomValue : BaseEntity<int>
{
    public int ShowId { get; set; }
    public int CriteriaId { get; set; }
    // Stored as JSON string to support all data types (string, number, boolean, array)
    public string Value { get; set; } = string.Empty;
}
