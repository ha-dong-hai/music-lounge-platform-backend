// CoreFlow: CF2 (Event Discovery)
// How the user's preference for a custom criteria was determined.
namespace MusicLounge.Domain.Enums;

public enum UserPreferenceSource
{
    // User explicitly selected this preference in their profile settings
    Explicit = 1,
    // AI inferred this preference from user behaviour log (ai_consent required)
    Learned = 2
}
