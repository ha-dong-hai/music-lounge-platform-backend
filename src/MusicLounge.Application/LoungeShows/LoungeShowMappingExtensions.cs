using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Common;
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
        // MLACP-388: gia chua duyet khong phai gia dang ban — khong dua vao khoang gia hien cho nguoi mua.
        var prices = show.TicketTiers.SelectMany(t => t.Prices).Where(p => p.IsActive).ToList();
        return new LoungeShowListItemDto(
            show.Id,
            show.Name,
            show.DisplayImageUrl(),
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

    /// <param name="lastEntryMinutes">
    /// BR-31: buổi diễn phải còn lại ít nhất bấy nhiêu phút thì vé mới còn được bán. Truyền vào để
    /// mốc đóng bán hiển thị cho khán giả đúng bằng mốc mà hệ thống thật sự chặn ở khâu thanh
    /// toán — hai con số đó lệch nhau là cách nhanh nhất để mất lòng tin.
    /// </param>
    internal static LoungeShowDetailDto ToDetailDto(
        this LoungeShow show, IReadOnlySet<int> wishlistedIds,
        bool? userHasTicket = null, bool? userHasRated = null,
        IReadOnlyDictionary<int, int>? soldAndHeld = null,
        IReadOnlyList<LoungeGalleryImageDto>? galleryImages = null,
        int lastEntryMinutes = TicketSaleWindow.DefaultLastEntryMinutes)
        => ToDetailDtoCore(
            show, wishlistedIds, userHasTicket, userHasRated, soldAndHeld, galleryImages,
            TicketSaleWindow.LastEntry(show, lastEntryMinutes));

    private static LoungeShowDetailDto ToDetailDtoCore(
        LoungeShow show, IReadOnlySet<int> wishlistedIds,
        bool? userHasTicket, bool? userHasRated,
        IReadOnlyDictionary<int, int>? soldAndHeld,
        IReadOnlyList<LoungeGalleryImageDto>? galleryImages,
        DateTimeOffset lastEntry)
        => new(show.Id, show.Name, show.Description, show.DisplayImageUrl(),
               show.ScheduledStart, show.ScheduledEnd, show.Format, show.Status,
               show.Status == LoungeShowStatus.Ongoing,
               show.Livestream?.Id,
               show.Lounge.ToSummaryDto(galleryImages ?? []),
               show.Performances.OrderBy(p => p.OrderIndex)
                   .Select(p => p.Performer.ToSummaryDto(p.Id, p.AcceptsDonation, p.Role, p.SetTime)).ToList(),
               // MLACP-388: an hang ve ma moi gia deu dang cho duyet — nguoi mua khong mua duoc no.
               show.TicketTiers.Where(t => t.Prices.Count == 0 || t.Prices.Any(p => p.IsActive))
                   .Select(t => t.ToSummaryDto(soldAndHeld, lastEntry)).ToList(),
               show.Genres.Select(g => new GenreDto(g.Genre.Id, g.Genre.Name)).ToList(),
               show.Moods.Select(m => new MoodDto(m.Mood.Id, m.Mood.Name)).ToList(),
               show.Atmospheres.Select(a => new AtmosphereDto(a.Atmosphere.Id, a.Atmosphere.Name)).ToList(),
               show.Ratings.ToRatingSummaryDto(),
               show.Ratings.ToFeaturedRatingDtos(),
               wishlistedIds.Contains(show.Id),
               userHasTicket,
               userHasRated,
               show.LegalApprovalConfirmedAt.HasValue,
               show.PlaybackMode,
               show.ToRefundPolicyDto(),
               show.TicketSaleClosesAt);

    /// <summary>
    /// Built from TicketRefundPolicy, the same resolver CancelTicket uses to decide what a buyer
    /// actually gets — so what the show page promises and what the cancel endpoint does cannot
    /// drift apart.
    /// </summary>
    /// <summary>
    /// Ảnh đại diện của buổi hoà nhạc.
    ///
    /// LoungeShow có hai cột ảnh, và trước MLACP-300 mọi DTO đều đọc CoverImageUrl — cột mà KHÔNG
    /// CHỖ NÀO GHI. Cả SetShowPoster lẫn phần sinh poster bằng AI đều ghi vào PosterUrl. Nghĩa là
    /// chủ phòng trà tải poster lên rồi không màn hình nào hiển thị nó, và mọi buổi diễn ở mọi danh
    /// sách đều trả về ảnh rỗng.
    ///
    /// Để dạng ưu tiên chứ không thay thẳng bằng PosterUrl: nếu sau này có người nối đường ghi cho
    /// ảnh bìa riêng thì nó thắng, còn hôm nay thì poster được hiển thị. Tên trường trong response
    /// giữ nguyên nên phía client không phải đổi gì.
    /// </summary>
    internal static string? DisplayImageUrl(this LoungeShow show)
        => show.CoverImageUrl ?? show.PosterUrl;

    internal static TicketRefundPolicyDto ToRefundPolicyDto(this LoungeShow show)
    {
        var terms = TicketRefundPolicy.Resolve(show);
        return new TicketRefundPolicyDto(
            terms.CancellationAllowed,
            terms.RefundPercentage,
            terms.CancelBefore,
            terms.DeadlineHoursBeforeStart,
            terms.AlwaysFullRefundIfVenueCancels,
            TicketRefundPolicy.Describe(terms));
    }

    internal static RecommendedLoungeShowDto ToRecommendedDto(
        this LoungeShow show, float score, string reason)
    {
        // MLACP-388: gia chua duyet khong phai gia dang ban — khong dua vao khoang gia hien cho nguoi mua.
        var prices = show.TicketTiers.SelectMany(t => t.Prices).Where(p => p.IsActive).ToList();
        return new RecommendedLoungeShowDto(
            show.Id, show.Name, show.DisplayImageUrl(),
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

    private static LoungeSummaryDto ToSummaryDto(
        this Domain.Entities.MusicLounge lounge, IReadOnlyList<LoungeGalleryImageDto> galleryImages)
        => new(lounge.Id, lounge.Name,
               lounge.Address.Street, lounge.Address.Ward,
               lounge.Address.District, lounge.Address.City,
               lounge.Address.FullAddress,
               lounge.Address.Latitude, lounge.Address.Longitude,
               lounge.PrimaryImageUrl,
               lounge.Model3DUrl,
               lounge.Atmosphere?.Name,
               galleryImages);

    private static PerformerSummaryDto ToSummaryDto(
        this Performer performer, int performanceId, bool acceptsDonation,
        PerformerRole role, TimeOnly? setTime)
        => new(performer.Id, performer.Name, performer.AvatarUrl, performer.Bio,
               performer.Genres.Select(g => new GenreDto(g.Genre.Id, g.Genre.Name)).ToList(),
               performanceId, acceptsDonation, role, setTime);

    private static TicketTierSummaryDto ToSummaryDto(
        this TicketTier tier, IReadOnlyDictionary<int, int>? soldAndHeld, DateTimeOffset lastEntry)
        => new(tier.Id, tier.Name, tier.Description, tier.AccessType, tier.TotalCapacity, tier.ZoneId,
               tier.Prices.Where(p => p.IsActive).Select(p => p.ToSummaryDto(soldAndHeld, lastEntry)).ToList());

    /// <param name="lastEntry">
    /// BR-31: giờ nhận khách cuối của buổi diễn. Không đợt bán nào đóng muộn hơn mốc này, và đợt
    /// nào không đặt mốc riêng thì lấy thẳng mốc này. Truyền xuống tận đây để trường SaleEnd trả
    /// về vẫn luôn là một mốc có thật — FE không phải đoán, và không phải đổi kiểu dữ liệu.
    /// </param>
    private static TicketPriceSummaryDto ToSummaryDto(
        this TicketPrice price, IReadOnlyDictionary<int, int>? soldAndHeld, DateTimeOffset lastEntry)
    {
        int? availableSlots = price.Quota.HasValue
            ? Math.Max(0, price.Quota.Value - (soldAndHeld?.GetValueOrDefault(price.Id, 0) ?? 0))
            : null;
        var effectiveEnd = price.SaleEnd is { } explicitEnd && explicitEnd < lastEntry
            ? explicitEnd
            : lastEntry;
        return new(price.Id, price.Name, price.Price, price.Quota,
                   price.SaleStart, effectiveEnd, price.PurchaseChannel,
                   availableSlots, price.SaleEnd != effectiveEnd);
    }

    private static RatingSummaryDto ToRatingSummaryDto(
        this ICollection<LoungeShowRating> ratings)
        => ratings.Count == 0
            ? new RatingSummaryDto(0, 0)
            : new RatingSummaryDto(ratings.Average(r => r.Score), ratings.Count);

    // MLACP-60: chi lay danh gia con hieu luc (chua bi go), co binh luan, diem cao nhat truoc —
    // ratings 5 sao khong binh luan gi khong dang hien thi thanh "danh gia noi bat" tren trang cong khai.
    private static IReadOnlyList<FeaturedRatingDto> ToFeaturedRatingDtos(
        this ICollection<LoungeShowRating> ratings)
        => ratings
            .Where(r => !r.IsRemoved && !string.IsNullOrWhiteSpace(r.Comment) && r.User is not null)
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r => new FeaturedRatingDto(
                r.Score, r.Comment!, r.User!.FullName, r.User.AvatarUrl, r.CreatedAt))
            .ToList();
}
