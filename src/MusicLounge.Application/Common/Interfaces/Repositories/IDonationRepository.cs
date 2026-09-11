using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface IDonationRepository : IRepository<Donation, int>
{
    /// <summary>
    /// MLACP-365: trả dữ liệu thô — <c>PublicDonationStatement</c> quyết định trường nào được công khai.
    /// Gồm cả donate nền tảng đang giữ (PendingOwnerAck): tiền đã thu thật.
    /// </summary>
    Task<PaginatedResult<PublicDonationRow>> GetPublicHistoryByPerformerAsync(
        int performerId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>MLACP-365: mọi khoản công khai của nghệ sĩ, cho phần tổng hợp.</summary>
    Task<IReadOnlyList<PublicDonationRow>> ListPublicByPerformerAsync(int performerId, CancellationToken ct = default);

    /// <summary>
    /// Single JOIN query returning the venue OwnerId, LoungeShowId, and PerformerId for a
    /// donation. Returns null when the donation does not exist.
    /// </summary>
    Task<(int OwnerId, int LoungeShowId, int PerformerId)?> GetOwnershipInfoAsync(int donationId, CancellationToken ct = default);

    /// <summary>
    /// Computes the DTO's AmountToPayPerformer preview from each donation's own
    /// <c>PerformerShareRateSnapshot</c> — the same value ConfirmDonationPaidCommandHandler will
    /// actually use at confirmation time, so this preview can never show a different figure than
    /// what actually gets transferred. <paramref name="fallbackPerformerShareRate"/> (a live
    /// system_config read) only applies to donations created before that column existed.
    /// </summary>
    Task<PaginatedResult<PendingDonationDto>> GetPendingForOwnerAsync(
        int ownerId, decimal fallbackPerformerShareRate, int page, int pageSize, CancellationToken ct = default);

    Task<PaginatedResult<PendingDonationDto>> GetOwnerReceivedAwaitingPayoutAsync(
        int ownerId, decimal fallbackPerformerShareRate, int page, int pageSize, CancellationToken ct = default);

    Task<PaginatedResult<MyDonationDto>> GetMyDonationsAsync(
        int userId, int page, int pageSize, CancellationToken ct = default);
}
