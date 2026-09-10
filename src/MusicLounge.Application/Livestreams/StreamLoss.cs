using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Livestreams;

public enum StreamLossOutcome
{
    /// <summary>Buổi diễn được đóng cùng với stream.</summary>
    ShowEnded,

    /// <summary>Show Hybrid: stream dừng nhưng phòng thật vẫn diễn — buổi diễn giữ Ongoing.</summary>
    ShowKeptOpenForRoom,

    /// <summary>Buổi diễn đã ở trạng thái cuối (đã huỷ, đã kết thúc) — không đụng tới.</summary>
    AlreadyTerminal
}

/// <summary>
/// MLACP-353 — stream dừng <b>ngoài ý muốn</b> (mất kết nối quá hạn chờ, bị Admin gỡ, bị kiểm duyệt
/// gỡ) thì làm gì với buổi diễn.
///
/// <para><b>Trước đây</b> cả ba đường đều đóng luôn buổi diễn. Đúng với show Online — stream là toàn
/// bộ buổi diễn. Sai với show <c>Hybrid</c>: phòng thật vẫn đang diễn, nhưng hệ thống nói đã kết thúc,
/// nên khán giả tới muộn không check-in được (<c>CheckInTicket</c> đòi <c>Ongoing</c>) và vì vậy mất cả
/// quyền đánh giá. Và <c>EndLoungeShow</c> từ chối mọi show có livestream, nên nhân viên không có nút
/// nào để đóng buổi diễn cho đúng lúc.</para>
///
/// <para><b>Nay</b> với Hybrid, stream dừng nhưng buổi diễn giữ <c>Ongoing</c> và chủ phòng trà được
/// báo để tự kết thúc buổi diễn khi xong (<c>EndLoungeShow</c> nay cho phép khi livestream đã dừng hẳn).
/// <c>AutoEndStaleShowsJob</c> vẫn là lưới an toàn nếu không ai bấm. Vé livestream của người mua không
/// phụ thuộc chuyện này: MLACP-347 đo thời lượng giao từ chính bản ghi livestream.</para>
///
/// <para><b>Không áp</b> cho hai đường <b>chủ động</b> — nhân viên bấm kết thúc stream, hoặc encoder tắt
/// hẳn khi đang Live — vẫn đóng cả buổi như trước: đó thường là lúc buổi diễn thật sự xong.</para>
///
/// <para>Một chỗ duy nhất cho cả ba đường, vì trước task này đường kiểm duyệt đã lệch khỏi hai đường
/// còn lại (đặt <c>Ended</c> thẳng thay vì qua <c>TryMarkEnded</c>).</para>
/// </summary>
public static class StreamLoss
{
    public static async Task<StreamLossOutcome> ApplyToShowAsync(
        IUnitOfWork uow, ISystemConfigService config, INotificationService notifications,
        LoungeShow show, string why, DateTimeOffset now, CancellationToken ct)
    {
        if (LoungeShowLifecycle.IsTerminal(show.Status))
            return StreamLossOutcome.AlreadyTerminal;

        if (show.Format != LoungeShowFormat.Hybrid)
        {
            var ratingWindowDays = await config.GetIntAsync(ConfigKeys.RatingWindowDays, 7, ct);
            LoungeShowLifecycle.TryMarkEnded(show, now, ratingWindowDays);   // §6.13
            uow.Repository<LoungeShow, int>().Update(show);
            return StreamLossOutcome.ShowEnded;
        }

        // Hybrid: chỉ stream dừng. Chủ phòng trà phải biết — không thì buổi diễn nằm Ongoing tới khi
        // job tự đóng chạy, và cửa sổ đánh giá mở trễ theo.
        var lounge = await uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct);
        if (lounge is not null)
        {
            await notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.LivestreamCutShort,
                "Livestream đã dừng — buổi diễn tại phòng vẫn mở",
                $"Livestream của \"{show.Name}\" đã dừng ({why}). Buổi diễn tại phòng vẫn đang mở để " +
                "khán giả tới muộn check-in được — hãy bấm \"Kết thúc buổi diễn\" khi buổi diễn xong.",
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);
        }

        return StreamLossOutcome.ShowKeptOpenForRoom;
    }
}
