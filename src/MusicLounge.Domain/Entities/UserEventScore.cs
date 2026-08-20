namespace MusicLounge.Domain.Entities;

// AI CF matrix — aggregated from user_behaviour_log (retained 6 months then aggregated here)
public sealed class UserEventScore
{
    public int UserId { get; set; }
    public int ShowId { get; set; }
    public decimal Score { get; set; } = 0m;
    public string? Breakdown { get; set; }  // JSON: {attended, rating, donated, wishlist, view}
    public DateTimeOffset ComputedAt { get; set; }

    public User User { get; set; } = null!;
    public LoungeShow Show { get; set; } = null!;
}
