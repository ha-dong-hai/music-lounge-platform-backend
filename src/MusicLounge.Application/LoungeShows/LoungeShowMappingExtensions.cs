using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows;

internal static class LoungeShowMappingExtensions
{
    internal static LoungeShowListItemDto ToListItemDto(
        this LoungeShow show,
        IReadOnlySet<int> wishlistedIds)
        => show.ToListItemDtoCore(wishlistedIds.Contains(show.Id));

    internal static LoungeShowListItemDto ToListItemDto(
        this LoungeShow show,
        bool? isWishlisted = null)
        => show.ToListItemDtoCore(isWishlisted);

    private static LoungeShowListItemDto ToListItemDtoCore(
        this LoungeShow show, bool? isWishlisted)
    {
        var prices = show.TicketTiers.SelectMany(t => t.Prices).ToList();
        return new LoungeShowListItemDto(
            show.Id,
            show.Name,
            show.CoverImageUrl,
            show.Lounge.Name,
            show.Lounge.Address.District,
            show.Lounge.Address.City,
            show.ScheduledStart,
            show.Format,
            show.Status,
            prices.Count > 0 ? prices.Min(p => p.Price) : null,
            prices.Count > 0 ? prices.Max(p => p.Price) : null,
            show.Genres.Select(g => new GenreDto(g.Genre.Id, g.Genre.Name)).ToList(),
            show.Performances.OrderBy(p => p.OrderIndex).Select(p => p.Performer.Name).ToList(),
            show.OfflineQuota,
            show.OnlineQuota,
            isWishlisted);
    }

    internal static LoungeShowDetailDto ToDetailDto(
        this LoungeShow show, IReadOnlySet<int> wishlistedIds,
        bool? userHasTicket = null, bool? userHasRated = null,
        IReadOnlyDictionary<int, int>? soldAndHeld = null)
        => new(show.Id, show.Name, show.Description, show.CoverImageUrl,
               show.ScheduledStart, show.ScheduledEnd, show.Format, show.Status,
               show.Status == LoungeShowStatus.Ongoing,
               show.Livestream?.Id,
               show.Lounge.ToSummaryDto(),
               show.Performances.OrderBy(p => p.OrderIndex)
                   .Select(p => p.Performer.ToSummaryDto(p.Id, p.AcceptsDonation)).ToList(),
               show.TicketTiers.Select(t => t.ToSummaryDto(soldAndHeld)).ToList(),
               show.Genres.Select(g => new GenreDto(g.Genre.Id, g.Genre.Name)).ToList(),
               show.Ratings.ToRatingSummaryDto(),
               wishlistedIds.Contains(show.Id),
               userHasTicket,
               userHasRated,
               show.LegalApprovalReference,
               show.LegalApprovalConfirmedAt.HasValue,
               show.VcpmcRoyaltyReference,
               show.PlaybackMode);

    internal static RecommendedLoungeShowDto ToRecommendedDto(
        this LoungeShow show, float score, string reason)
    {
        var prices = show.TicketTiers.SelectMany(t => t.Prices).ToList();
        return new RecommendedLoungeShowDto(
            show.Id, show.Name, show.CoverImageUrl,
            show.Lounge.Name, show.Lounge.Address.District, show.Lounge.Address.City,
            show.ScheduledStart, show.Format, show.Status,
            prices.Count > 0 ? prices.Min(p => p.Price) : null,
            prices.Count > 0 ? prices.Max(p => p.Price) : null,
            show.Genres.Select(g => new GenreDto(g.Genre.Id, g.Genre.Name)).ToList(),
            show.Performances.OrderBy(p => p.OrderIndex).Select(p => p.Performer.Name).ToList(),
            score, reason);
    }

    internal static PerformerDetailDto ToDetailDto(
        this Performer performer,
        PaginatedResult<LoungeShowListItemDto> shows)
        => new(performer.Id, performer.Name, performer.AvatarUrl, performer.Bio,
               performer.Genres.Select(g => new GenreDto(g.Genre.Id, g.Genre.Name)).ToList(),
               shows);

    private static LoungeSummaryDto ToSummaryDto(this Domain.Entities.MusicLounge lounge)
        => new(lounge.Id, lounge.Name,
               lounge.Address.Street, lounge.Address.Ward,
               lounge.Address.District, lounge.Address.City,
               lounge.Address.FullAddress,
               lounge.Address.Latitude, lounge.Address.Longitude,
               lounge.PrimaryImageUrl,
               lounge.Model3DUrl);

    private static PerformerSummaryDto ToSummaryDto(this Performer performer, int performanceId, bool acceptsDonation)
        => new(performer.Id, performer.Name, performer.AvatarUrl, performer.Bio,
               performer.Genres.Select(g => new GenreDto(g.Genre.Id, g.Genre.Name)).ToList(),
               performanceId, acceptsDonation);

    private static TicketTierSummaryDto ToSummaryDto(
        this TicketTier tier, IReadOnlyDictionary<int, int>? soldAndHeld)
        => new(tier.Id, tier.Name, tier.Description, tier.AccessType, tier.TotalCapacity, tier.ZoneId,
               tier.Prices.Select(p => p.ToSummaryDto(soldAndHeld)).ToList());

    private static TicketPriceSummaryDto ToSummaryDto(
        this TicketPrice price, IReadOnlyDictionary<int, int>? soldAndHeld)
    {
        int? availableSlots = price.Quota.HasValue
            ? Math.Max(0, price.Quota.Value - (soldAndHeld?.GetValueOrDefault(price.Id, 0) ?? 0))
            : null;
        return new(price.Id, price.Name, price.Price, price.Quota,
                   price.SaleStart, price.SaleEnd, price.PurchaseChannel,
                   availableSlots);
    }

    private static RatingSummaryDto ToRatingSummaryDto(
        this ICollection<LoungeShowRating> ratings)
        => ratings.Count == 0
            ? new RatingSummaryDto(0, 0)
            : new RatingSummaryDto(ratings.Average(r => r.Score), ratings.Count);
}
