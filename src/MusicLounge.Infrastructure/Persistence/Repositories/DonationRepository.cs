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
                // MLACP-362: han tu xac nhan do handler dien theo DonationPayoutDeadline — truoc day la
                // +24h hardcode trong khi job that su cho donation_hold_days.
                null, null, null))
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
                // MLACP-362: truoc day o nay chua OwnerAckAt du ten truong la PaymentConfirmedAt.
                d.PaymentConfirmedAt,
                null, null, null))
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

    public async Task<PaginatedResult<PublicDonationRow>> GetPublicHistoryByPerformerAsync(
        int performerId, int page, int pageSize, CancellationToken ct = default)
    {
        var baseQuery = PublicByPerformer(performerId);

        var total = await baseQuery.CountAsync(ct);

        var items = await ToPublicRows(baseQuery
                .OrderByDescending(d => d.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(ct);

        return new PaginatedResult<PublicDonationRow>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<PublicDonationRow>> ListPublicByPerformerAsync(
        int performerId, CancellationToken ct = default)
        => await ToPublicRows(PublicByPerformer(performerId).OrderByDescending(d => d.Id)).ToListAsync(ct);

    // MLACP-365: tinh ca donate nen tang dang giu (PendingOwnerAck) — tien da thu that, truoc day khong
    // hien tren trang cong khai cho toi khi chu phong tra bam "da nhan".
    private IQueryable<Donation> PublicByPerformer(int performerId)
        => _ctx.Donations
            .AsNoTracking()
            .Where(d => d.Performance.PerformerId == performerId
                && (d.Status == DonationStatus.PendingOwnerAck
                    || d.Status == DonationStatus.OwnerReceived
                    || d.Status == DonationStatus.PerformerPaid));

    private static IQueryable<PublicDonationRow> ToPublicRows(IQueryable<Donation> query)
        => query.Select(d => new PublicDonationRow(
            d.Id,
            d.Performance.LoungeShow.Name,
            d.Performance.LoungeShow.Lounge.Name,
            d.Performance.LoungeShow.ScheduledStart,
            d.IsAnonymous ? null : d.DisplayName,
            d.IsAmountPublic,
            d.Gross,
            d.Net,
            d.PerformerShareRateSnapshot,
            d.Status,
            d.AutoConfirmed,
            d.PaymentConfirmedAt,
            d.OwnerAckAt,
            d.OwnerPaidAt,
            d.Message,
            d.IsMessagePublic,
            d.MessageHiddenAt,
            d.PaymentEvidenceUrl != null && d.PaymentEvidenceUrl != "",
            d.CreatedAt));
}
