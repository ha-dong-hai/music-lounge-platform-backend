using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Common;

/// <summary>
/// CF1. Một phòng trà chỉ có một sân khấu, nên nó không thể chạy hai buổi diễn chồng giờ nhau —
/// nhưng không có chỗ nào trong hệ thống kiểm điều đó. Ba đường ghi lịch (tạo mới, sửa bản nháp,
/// đổi lịch) và một đường nộp duyệt đều đặt được giờ diễn mà không ai hỏi giờ đó đã có ai giữ chưa.
///
/// Hậu quả không dừng ở bảng lịch xấu: hai buổi diễn cùng khung giờ đều mở bán vé thật, và đến
/// đúng tối đó thì một trong hai bên khán giả sẽ phải bị huỷ và hoàn tiền. Nghĩa là hệ thống bán
/// một thứ mà nó biết chắc là không giao được — chỉ có điều nó chưa từng được dạy cách biết.
/// </summary>
public static class ShowScheduleConflict
{
    /// <param name="excludeShowId">
    /// Buổi diễn đang được sửa/nộp duyệt. Không loại nó ra thì nó tự đụng chính mình.
    /// </param>
    public static async Task EnsureVenueIsFreeAsync(
        IUnitOfWork uow,
        ISystemConfigService config,
        int loungeId,
        int? excludeShowId,
        DateTimeOffset start,
        DateTimeOffset? end,
        CancellationToken ct)
    {
        var effectiveEnd = ShowSchedule.EffectiveEnd(start, end);

        // CF1 (MLACP-310): hai suất không chỉ phải không giẫm lên nhau, mà còn phải cách nhau đủ
        // để đưa khán giả suất trước ra và nhận khán giả suất sau vào. Nới khoảng bận của mỗi buổi
        // diễn ra hai phía đúng bằng khoảng đó, rồi dùng lại phép so trùng cũ.
        var changeover = TimeSpan.FromMinutes(await config.GetIntAsync(
            ConfigKeys.VenueChangeoverMinutes, VenueChangeover.DefaultMinutes, ct));

        // Lọc bằng LoungeId + Status ở phía database (đã có sẵn index (LoungeId, Status)), rồi so
        // sánh thời gian trong bộ nhớ. So sánh DateTimeOffset trong cùng một truy vấn với phép so
        // enum là đúng cái tổ hợp không dịch được sang SQLite — provider mà bộ test đang chạy —
        // và đây là lớp lỗi đã có tiền lệ trong codebase này. Tập lấy về nhỏ: buổi diễn đã qua
        // chuyển sang Ended/Cancelled nên rơi khỏi danh sách giữ chỗ.
        var committed = await uow.Repository<LoungeShow, int>().FindAsync(
            s => s.LoungeId == loungeId && ShowSchedule.OccupiesTheVenue.Contains(s.Status), ct);

        var clash = committed.FirstOrDefault(s =>
            (excludeShowId is null || s.Id != excludeShowId.Value)
            && ShowSchedule.Overlaps(
                start, effectiveEnd,
                s.ScheduledStart - changeover, ShowSchedule.EffectiveEnd(s) + changeover));

        if (clash is null) return;

        var clashEnd = ShowSchedule.EffectiveEnd(clash);
        var overlaps = ShowSchedule.Overlaps(start, effectiveEnd, clash.ScheduledStart, clashEnd);

        // Nói đúng vướng cái gì. "Trùng giờ" và "sát giờ quá" là hai tình huống khác hẳn nhau đối
        // với người đang xếp lịch: một cái phải đổi hẳn ngày, một cái chỉ cần dịch ra nửa tiếng.
        throw new ConflictException(overlaps
            ? $"Phòng trà đã có buổi diễn \"{clash.Name}\" trong khung giờ này " +
              $"({Vn(clash.ScheduledStart)} – {Vn(clashEnd)}). Vui lòng chọn khung giờ khác."
            : $"Buổi diễn này quá sát với \"{clash.Name}\" ({Vn(clash.ScheduledStart)} – " +
              $"{Vn(clashEnd)}). Hai buổi diễn liên tiếp cần cách nhau ít nhất " +
              $"{changeover.TotalMinutes:0} phút để đưa khán giả suất trước ra và đón suất sau vào.");
    }

    /// <summary>
    /// Giờ Việt Nam, vì người đọc thông báo này đang đứng ở phòng trà đó. Hệ thống lưu mốc thời
    /// gian theo UTC, nên đưa nguyên UTC ra sẽ lệch 7 tiếng so với cái lịch họ đang nhìn.
    /// </summary>
    private static string Vn(DateTimeOffset value)
        => VietnamTime.Format(value);
}
