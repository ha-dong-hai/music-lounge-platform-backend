using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Queries.GetShowSeatingMap;

internal sealed class GetShowSeatingMapQueryHandler : IRequestHandler<GetShowSeatingMapQuery, SeatingMapDto>
{
    private readonly ILoungeShowRepository _showRepo;
    private readonly IUnitOfWork _uow;
    private readonly ITicketRepository _ticketRepo;
    private readonly ICurrentUserService _currentUser;

    public GetShowSeatingMapQueryHandler(
        ILoungeShowRepository showRepo, IUnitOfWork uow, ITicketRepository ticketRepo,
        ICurrentUserService currentUser)
    {
        _showRepo = showRepo;
        _uow = uow;
        _ticketRepo = ticketRepo;
        _currentUser = currentUser;
    }

    public async Task<SeatingMapDto> Handle(GetShowSeatingMapQuery request, CancellationToken ct)
    {
        var show = await _showRepo.GetByIdWithDetailsAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.LoungeShow), request.ShowId);

        // Endpoint AllowAnonymous (khop show-detail cong khai) — nhung show Draft van phai an
        // giong het GetLoungeShowDetailQueryHandler, khong duoc lo layout/gia qua endpoint rieng nay.
        // Cross-venue Staff bypass fixed the same way as GetLoungeShowDetailQueryHandler: Admin
        // bypasses fully, Staff/Owner must actually operate THIS show's venue.
        if (show.Status == LoungeShowStatus.Draft
            && !VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, show.Lounge.OwnerId))
            throw new NotFoundException(nameof(Domain.Entities.LoungeShow), request.ShowId);

        // Chi gom tier co ZoneId (tier online/khong phan khu giu nguyen UX danh sach phang cu,
        // khong xuat hien tren ban do). AvailableSlots doc qua ITicketRepository.GetReservedQuantitiesByPriceIdsAsync
        // — dung CUNG 1 nguon voi GetTicketTiersQueryHandler nen so tren ban do va so tren danh
        // sach gia (cung 1 man hinh) khong bao gio lech nhau.
        var tiersByZone = show.TicketTiers
            .Where(t => t.ZoneId.HasValue)
            .GroupBy(t => t.ZoneId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        if (tiersByZone.Count == 0)
            return new SeatingMapDto(show.Lounge.AreaLayoutImageUrl, []);

        var zoneIds = tiersByZone.Keys.ToList();
        var zones = await _uow.Repository<SeatingZone, int>()
            .FindAsync(z => zoneIds.Contains(z.Id), ct);
        var zonesById = zones.ToDictionary(z => z.Id);

        // Live-computed từ tickets + holds thực tế, cùng nguồn với GetTicketTiersQueryHandler —
        // TicketPrice.Sold không được dùng vì cột đó không bao giờ được ghi (luôn = 0).
        var allPriceIds = tiersByZone.Values
            .SelectMany(tiers => tiers.SelectMany(t => t.Prices))
            .Select(p => p.Id)
            .Distinct()
            .ToList();
        var reserved = await _ticketRepo.GetReservedQuantitiesByPriceIdsAsync(allPriceIds, ct);

        var entries = new List<ZoneMapEntryDto>();
        foreach (var (zoneId, tiers) in tiersByZone)
        {
            // Zone co the da bi Owner deactivate SAU KHI show da co tier tham chieu toi — van phai
            // hien vi show da publish/ban ve dua tren zone do, khong duoc bien mat khoi ban do.
            if (!zonesById.TryGetValue(zoneId, out var zone))
                continue;

            var prices = tiers.SelectMany(t => t.Prices).ToList();
            if (prices.Count == 0)
                continue;

            var hasUnlimited = prices.Any(p => !p.Quota.HasValue);
            int? availableCount = hasUnlimited
                ? null
                : prices.Sum(p => Math.Max(0, p.Quota!.Value - reserved[p.Id]));

            entries.Add(new ZoneMapEntryDto(
                zone.Id,
                zone.Name,
                zone.Capacity,
                zone.LayoutColor,
                zone.Layout2DX, zone.Layout2DY, zone.Layout2DWidth, zone.Layout2DHeight, zone.Layout2DRotationDeg,
                zone.Layout3DX, zone.Layout3DY, zone.Layout3DZ,
                availableCount,
                prices.Min(p => p.Price),
                prices.Max(p => p.Price)));
        }

        return new SeatingMapDto(
            show.Lounge.AreaLayoutImageUrl,
            entries.OrderBy(e => zonesById[e.ZoneId].DisplayOrder).ToList());
    }
}
