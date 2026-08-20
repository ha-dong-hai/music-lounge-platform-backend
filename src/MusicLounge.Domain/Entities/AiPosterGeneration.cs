using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// Every AI poster generation attempt for a show — a log, not just the current poster. Exists so
// (a) SubscriptionPackage.MaxAiPostersPerMonth can be enforced accurately (only Succeeded rows
// count against an Owner's monthly quota — a Failed attempt is the vendor's fault, not theirs, so
// it must never cost them a poster), and (b) there's an auditable record to point to if an Owner
// disputes "I paid for posters I never got" — the log shows exactly which attempts failed and why,
// distinct from ones that succeeded but the Owner simply regenerated over.
public sealed class AiPosterGeneration : Common.BaseEntity<int>
{
    public int ShowId { get; set; }
    public int OwnerId { get; set; }
    public AiPosterGenerationStatus Status { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public LoungeShow Show { get; set; } = null!;
    public User Owner { get; set; } = null!;
}
