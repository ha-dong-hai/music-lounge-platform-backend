namespace MusicLounge.Api.Authorization;

public static class Policies
{
    public const string RequireAuthenticated = "RequireAuthenticated";
    public const string RequireStaff = "RequireStaff";
    public const string RequireOwner = "RequireOwner";
    public const string RequireAdmin = "RequireAdmin";

    // Day-to-day venue operation (check-in, walk-in sale, start/end show, F&B status, livestream
    // control) — anyone who can actually run the venue floor: assigned Staff, the venue's own
    // Owner (small venues often have no separate Staff account at all), or Admin. Separate from
    // RequireOwner, which gates venue *management* (create/edit lounge, staffing, ticket tiers).
    public const string RequireVenueOperator = "RequireVenueOperator";
}
