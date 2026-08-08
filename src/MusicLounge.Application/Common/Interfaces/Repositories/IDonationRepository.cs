using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface IDonationRepository : IRepository<Donation, int>
{
    Task<PaginatedResult<PublicDonationDto>> GetPublicHistoryByPerformerAsync(
        int performerId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Single JOIN query returning the venue OwnerId, LoungeShowId, and PerformerId for a
    /// donation. Returns null when the donation does not exist.
    /// </summary>
    Task<(int OwnerId, int LoungeShowId, int PerformerId)?> GetOwnershipInfoAsync(int donationId, CancellationToken ct = default);

    Task<PaginatedResult<PendingDonationDto>> GetPendingForOwnerAsync(
        int ownerId, int page, int pageSize, CancellationToken ct = default);

    Task<PaginatedResult<PendingDonationDto>> GetOwnerReceivedAwaitingPayoutAsync(
        int ownerId, int page, int pageSize, CancellationToken ct = default);

    Task<PaginatedResult<MyDonationDto>> GetMyDonationsAsync(
        int userId, int page, int pageSize, CancellationToken ct = default);
}
