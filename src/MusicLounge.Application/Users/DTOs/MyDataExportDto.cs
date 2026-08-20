namespace MusicLounge.Application.Users.DTOs;

// 91/2025/QH15 (DSAR): data-portability half of governance gap #1 — self-service export of the
// PII this platform actually holds about the requesting user, assembled synchronously so the
// 2-day acknowledgement requirement is trivially met (the response IS the acknowledgement).
// Erasure (the 15-day processing half) is NOT covered here — see note in the query handler.
public sealed record MyDataExportDto(
    ExportedProfile Profile,
    IReadOnlyList<ExportedTicket> Tickets,
    IReadOnlyList<ExportedDonation> Donations,
    IReadOnlyList<ExportedRating> Ratings,
    IReadOnlyList<ExportedComplaint> Complaints,
    IReadOnlyList<int> FollowedLoungeIds,
    IReadOnlyList<int> WishlistedShowIds);

public sealed record ExportedProfile(
    int Id, string Email, string FullName, string? Phone, DateTime CreatedAt);

public sealed record ExportedTicket(
    Guid Id, int ShowId, string Status, DateTimeOffset CreatedAt);

public sealed record ExportedDonation(
    int Id, decimal Gross, string Status, DateTimeOffset CreatedAt);

public sealed record ExportedRating(int ShowId, int Score, string? Comment, DateTimeOffset CreatedAt);

public sealed record ExportedComplaint(
    int Id, string Category, string Status, DateTimeOffset CreatedAt);
