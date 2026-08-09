using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class DonationRepository : Repository<Donation, int>, IDonationRepository
{
    private readonly ApplicationDbContext _ctx;

    public DonationRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    public async Task<(int OwnerId, int LoungeShowId, int PerformerId)?> GetOwnershipInfoAsync(
        int donationId, CancellationToken ct = default)
    {
        var row = await _ctx.Donations
            .AsNoTracking()
            .Where(d => d.Id == donationId)
            .Select(d => new
            {
                OwnerId = d.Performance.LoungeShow.Lounge.OwnerId,
                LoungeShowId = d.Performance.LoungeShowId,
                PerformerId = d.Performance.PerformerId
            })
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;
        return (row.OwnerId, row.LoungeShowId, row.PerformerId);
    }

    public async Task<PaginatedResult<PendingDonationDto>> GetPendingForOwnerAsync(
        int ownerId, decimal fallbackPerformerShareRate, int page, int pageSize, CancellationToken ct = default)
    {
        var baseQuery = _ctx.Donations
            .AsNoTracking()
            .Where(d => d.Performance.LoungeShow.Lounge.OwnerId == ownerId
                && d.Status == DonationStatus.PendingOwnerAck);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new PendingDonationDto(
                d.Id,
                d.Performance.Performer.Name,
                d.Performance.LoungeShow.Name,
                d.Gross,
                d.Net,
                Math.Round(d.Gross * (d.PerformerShareRateSnapshot ?? fallbackPerformerShareRate), 2),
                d.IsAnonymous,
                d.IsAnonymous ? null : d.DisplayName,
                d.IsMessagePublic ? d.Message : null,
                d.PaymentConfirmedAt,
                d.PaymentConfirmedAt != null
                    ? d.PaymentConfirmedAt.Value.AddHours(24)
                    : (DateTimeOffset?)null))
            .ToListAsync(ct);

        return new PaginatedResult<PendingDonationDto>(items, page, pageSize, total);
    }

    public async Task<PaginatedResult<PendingDonationDto>> GetOwnerReceivedAwaitingPayoutAsync(
        int ownerId, decimal fallbackPerformerShareRate, int page, int pageSize, CancellationToken ct = default)
    {
        var baseQuery = _ctx.Donations
            .AsNoTracking()
            .Where(d => d.Performance.LoungeShow.Lounge.OwnerId == ownerId
                && d.Status == DonationStatus.OwnerReceived);

        var total = await baseQuery.CountAsync(ct);
        // OrderBy(d => d.OwnerAckAt) (DateTimeOffset) doesn't translate under the SQLite provider
        // used in tests (same class of issue documented elsewhere in this codebase — Lounges,
        // Recommendations, TicketTransferExpiry, GetChatMessagesAsync) — a real 500 caught only
        // once this endpoint actually got test coverage. Unlike those call sites, ordering by Id
        // isn't a safe substitute here (owners can acknowledge donations out of creation order,
        // so Id order != OwnerAckAt order) — page in memory instead. Awaiting-payout lists are
        // bounded per-owner (donations leave this status once paid out), not a growth-unbounded
        // log, so materializing the filtered set is an acceptable tradeoff.
        var all = await baseQuery
            .Select(d => new PendingDonationDto(
                d.Id,
                d.Performance.Performer.Name,
                d.Performance.LoungeShow.Name,
                d.Gross,
                d.Net,
                Math.Round(d.Gross * (d.PerformerShareRateSnapshot ?? fallbackPerformerShareRate), 2),
                d.IsAnonymous,
                d.IsAnonymous ? null : d.DisplayName,
                d.IsMessagePublic ? d.Message : null,
                d.OwnerAckAt,
                null))
            .ToListAsync(ct);

        var items = all
            .OrderBy(d => d.PaymentConfirmedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PaginatedResult<PendingDonationDto>(items, page, pageSize, total);
    }

    public async Task<PaginatedResult<MyDonationDto>> GetMyDonationsAsync(
        int userId, int page, int pageSize, CancellationToken ct = default)
    {
        var baseQuery = _ctx.Donations
            .AsNoTracking()
            .Where(d => d.DonorUserId == userId);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new MyDonationDto(
                d.Id,
                d.Performance.Performer.Name,
                d.Performance.LoungeShow.Name,
                d.Gross,
                d.Status.ToString(),
                d.IsAnonymous,
                d.Message,
                d.PaymentConfirmedAt,
                d.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedResult<MyDonationDto>(items, page, pageSize, total);
    }

    public async Task<PaginatedResult<PublicDonationDto>> GetPublicHistoryByPerformerAsync(
        int performerId, int page, int pageSize, CancellationToken ct = default)
    {
        var baseQuery = _ctx.Donations
            .AsNoTracking()
            .Where(d => d.Performance.PerformerId == performerId
                && (d.Status == DonationStatus.OwnerReceived || d.Status == DonationStatus.PerformerPaid));

        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new PublicDonationDto(
                d.Id,
                d.Performance.LoungeShow.Name,
                d.Performance.LoungeShow.Lounge.Name,
                d.Performance.LoungeShow.ScheduledStart,
                d.IsAnonymous ? null : d.DisplayName,
                d.IsAmountPublic ? (decimal?)d.Gross : null,
                d.Status.ToString(),
                d.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedResult<PublicDonationDto>(items, page, pageSize, total);
    }
}
