// CoreFlow: CF2 (Event Discovery)
// Data type of a venue-defined custom criteria field used in AI matching.
namespace MusicLounge.Domain.Enums;

public enum CustomCriteriaDataType
{
    // Dropdown — options stored as JSON array e.g. ["VI", "EN"]
    Select = 1,
    // Numeric range — options stored as JSON e.g. { min: 0, max: 100, step: 10 }
    Range = 2,
    Boolean = 3,
    Text = 4
}
