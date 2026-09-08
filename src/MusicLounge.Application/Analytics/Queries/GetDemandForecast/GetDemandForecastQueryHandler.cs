using MediatR;
using MusicLounge.Application.Analytics.Common;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Analytics.Queries.GetDemandForecast;

/// <summary>
/// Dự báo lượng vé bán ra của một buổi diễn đang mở bán, bằng nhịp bán so với các buổi diễn đã
/// hoàn tất. Phương pháp và lý do chọn nó nằm ở <see cref="SalesPacingForecaster"/>.
///
/// Việc của handler này là dựng đúng đầu vào cho phép tính đó: với mốc "còn N ngày nữa tới giờ
/// diễn" của buổi đang xét, mỗi buổi diễn cũ đóng góp một cặp số — tổng vé nó bán được, và số vé
/// nó đã bán được khi CHÍNH NÓ còn N ngày nữa tới giờ diễn. So cùng một mốc thì mới so được.
/// </summary>
internal sealed class GetDemandForecastQueryHandler
    : IRequestHandler<GetDemandForecastQuery, DemandForecastDto>
{
    /// <summary>
    /// Trần số buổi diễn cũ lấy về làm nền chung. Dự báo không khá hơn nhờ đọc thêm lịch sử từ ba
    /// năm trước, còn thói quen mua vé thì có đổi.
    /// </summary>
    private const int PlatformHistoryLimit = 100;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetDemandForecastQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<DemandForecastDto> Handle(GetDemandForecastQuery request, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền xem dự báo của buổi diễn này.");

        var now = DateTimeOffset.UtcNow;
        var leadTime = show.ScheduledStart - now;
        var daysUntilShow = (int)Math.Ceiling(leadTime.TotalDays);

        var soldSoFar = await CountSoldAsync(show.Id, null, ct);

        var capacity = await CapacityAsync(show.Id, ct);

        // Buổi diễn đã diễn ra thì không còn gì để dự báo — con số cuối cùng đã có thật.
        if (leadTime <= TimeSpan.Zero)
        {
            return new DemandForecastDto(
                show.Id, show.Name, nameof(ForecastStatus.Forecast),
                "Buổi diễn đã bắt đầu — đây là số vé bán được thực tế, không phải dự báo.",
                daysUntilShow, soldSoFar, soldSoFar, soldSoFar, soldSoFar,
                1m, 0m, 0, 0, capacity, SellThrough(soldSoFar, capacity));
        }

        var venueHistory = await BuildPaceAsync(
            await CompletedShowsAsync(show.LoungeId, show.Id, ct), leadTime, ct);
        var platformHistory = await BuildPaceAsync(
            await CompletedShowsAsync(null, show.Id, ct), leadTime, ct);

        var forecast = SalesPacingForecaster.Forecast(
            venueHistory, platformHistory, soldSoFar, capacity);

        return new DemandForecastDto(
            show.Id,
            show.Name,
            forecast.Status.ToString(),
            Explain(forecast, venueHistory.Count, platformHistory.Count, daysUntilShow),
            daysUntilShow,
            soldSoFar,
            forecast.ProjectedFinalSales,
            forecast.ProjectedLow,
            forecast.ProjectedHigh,
            forecast.ExpectedPaceFraction is decimal p ? Math.Round(p, 4) : null,
            Math.Round(forecast.VenueHistoryWeight, 4),
            venueHistory.Count,
            platformHistory.Count,
            capacity,
            SellThrough(forecast.ProjectedFinalSales, capacity));
    }

    /// <summary>
    /// Buổi diễn đã kết thúc, không tính chính buổi đang xét. Sắp theo khoá chính giảm dần: Id là
    /// identity tăng dần nên đó là "mới nhất trước", và provider SQLite dùng trong test không
    /// ORDER BY được cột DateTimeOffset — cùng lớp vấn đề đã xử ở MLACP-306.
    /// </summary>
    private async Task<IReadOnlyList<LoungeShow>> CompletedShowsAsync(
        int? loungeId, int excludeShowId, CancellationToken ct)
    {
        var shows = await _uow.Repository<LoungeShow, int>().FindAsync(
            s => s.Status == LoungeShowStatus.Ended
                && s.Id != excludeShowId
                && (loungeId == null || s.LoungeId == loungeId), ct);

        return shows.OrderByDescending(s => s.Id).Take(PlatformHistoryLimit).ToList();
    }

    /// <summary>
    /// Với mỗi buổi diễn cũ: tổng vé bán được, và số vé đã bán khi chính nó còn <paramref
    /// name="leadTime"/> nữa tới giờ diễn.
    /// </summary>
    private async Task<IReadOnlyList<ReferenceShowPace>> BuildPaceAsync(
        IReadOnlyList<LoungeShow> shows, TimeSpan leadTime, CancellationToken ct)
    {
        var result = new List<ReferenceShowPace>(shows.Count);
        foreach (var s in shows)
        {
            var final = await CountSoldAsync(s.Id, null, ct);
            if (final == 0) continue;

            var atLeadTime = await CountSoldAsync(s.Id, s.ScheduledStart - leadTime, ct);
            result.Add(new ReferenceShowPace(final, atLeadTime));
        }

        return result;
    }

    /// <summary>
    /// Vé "đã bán" là Confirmed hoặc Used — đúng định nghĩa GetTicketSalesTrend đang dùng. Hai màn
    /// hình nói về cùng một buổi diễn mà đếm khác nhau thì không màn hình nào còn đáng tin.
    /// </summary>
    private async Task<int> CountSoldAsync(int showId, DateTimeOffset? createdBefore, CancellationToken ct)
    {
        var tickets = await _uow.Repository<Ticket, Guid>().FindAsync(
            t => t.ShowId == showId
                && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used), ct);

        // Lọc theo thời điểm ở phía bộ nhớ: kết hợp so sánh DateTimeOffset với phép so enum trong
        // cùng một truy vấn là tổ hợp không dịch được sang SQLite.
        return createdBefore is DateTimeOffset cutoff
            ? tickets.Count(t => t.CreatedAt <= cutoff)
            : tickets.Count;
    }

    private async Task<int?> CapacityAsync(int showId, CancellationToken ct)
    {
        var tiers = await _uow.Repository<TicketTier, int>()
            .FindAsync(t => t.LoungeShowId == showId, ct);

        // Chỉ cộng được khi MỌI hạng vé đều khai sức chứa. Thiếu một hạng thì tổng đó nhỏ hơn sức
        // chứa thật, và dùng nó làm trần sẽ cắt cụt dự báo — thà không có trần còn hơn có trần sai.
        return tiers.Count > 0 && tiers.All(t => t.TotalCapacity.HasValue)
            ? tiers.Sum(t => t.TotalCapacity!.Value)
            : null;
    }

    private static decimal? SellThrough(int? projected, int? capacity)
        => projected is int p && capacity is int c && c > 0
            ? Math.Round((decimal)p / c, 4)
            : null;

    private static string Explain(
        PacingForecast forecast, int venueShows, int platformShows, int daysUntilShow)
        => forecast.Status switch
        {
            ForecastStatus.NotEnoughHistory =>
                $"Chưa đủ căn cứ để dự báo: cần ít nhất {SalesPacingForecaster.MinimumReferenceShows} " +
                $"buổi diễn đã hoàn tất để dựng nhịp bán tham chiếu, hiện mới có {platformShows}. " +
                "Đưa ra một con số lúc này là đoán, không phải dự báo.",

            ForecastStatus.TooEarly =>
                $"Còn {daysUntilShow} ngày nữa tới buổi diễn — ở mốc này các buổi diễn trước đó " +
                $"mới bán được khoảng {forecast.ExpectedPaceFraction:P0} tổng vé, quá ít để suy ra " +
                "con số cuối. Xem lại khi gần ngày diễn hơn.",

            _ when venueShows >= SalesPacingForecaster.MinimumReferenceShows =>
                $"Dựa trên {venueShows} buổi diễn đã hoàn tất của chính phòng trà này: tới mốc còn " +
                $"{daysUntilShow} ngày, các buổi đó thường đã bán được {forecast.ExpectedPaceFraction:P0} " +
                "tổng vé.",

            _ =>
                $"Phòng trà mới có {venueShows} buổi diễn đã hoàn tất nên chưa đủ để tự làm chuẩn; " +
                $"dự báo nghiêng {1 - forecast.VenueHistoryWeight:P0} về nhịp bán chung của " +
                $"{platformShows} buổi diễn trên nền tảng. Càng tổ chức thêm, dự báo càng bám sát " +
                "phòng trà của bạn."
        };
}
