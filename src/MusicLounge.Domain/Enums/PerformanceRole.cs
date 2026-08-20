// CoreFlow: CF1 (Event Management), CF2 (Event Discovery)
// Role of a performer in a specific show lineup.
namespace MusicLounge.Domain.Enums;

public enum PerformanceRole
{
    // Primary performer of the show
    Main = 1,
    // Special appearance alongside the main performer
    Guest = 2,
    // MC or event moderator
    Host = 3
}
