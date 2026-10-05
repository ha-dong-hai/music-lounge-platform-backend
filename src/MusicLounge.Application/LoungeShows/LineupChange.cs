using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Tickets;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Application.LoungeShows;

/// <summary>
/// MLACP-622. Đổi danh sách biểu diễn SAU khi buổi diễn đã gửi duyệt hoặc đã mở bán.
///
/// <b>Lỗi đã có:</b> thêm/sửa/xoá tiết mục chỉ được khi buổi diễn còn nháp. Ngoài đời ca sĩ ốm, kẹt lịch, thay người là
/// chuyện hằng tuần ở phòng trà; hệ thống không cho sửa nên phòng trà cứ để người khác lên hát trong khi trang bán vé vẫn
/// quảng cáo người cũ — người mua không được báo, không được chọn hoàn tiền.
///
/// <b>Căn cứ:</b>
/// <list type="bullet">
/// <item>NĐ 144/2020/NĐ-CP Điều 10 khoản 4 điểm d (đọc nguyên văn 04/10/2026): thay đổi nội dung đã được chấp thuận thì
/// tổ chức "có văn bản nêu rõ lý do" gửi cơ quan đã chấp thuận — nên đổi trên buổi đã mở bán phải ghi LÝ DO.</item>
/// <item>Luật BVQLNTD 2023: cung cấp dịch vụ khác nội dung đã công bố phải khắc phục, gồm chấm dứt và hoàn tiền
/// (cùng căn cứ MLACP-338 dùng cho buổi không diễn). Eventbrite cũng buộc hoàn khi sự kiện "khác đáng kể so với mô tả".</item>
/// </list>
///
/// <b>Thế nào là thay đổi BẤT LỢI cho người đã mua</b> (mở cửa sổ hoàn 100% + báo tin): bỏ một nghệ sĩ khỏi buổi đã mở
/// bán, hoặc hạ nghệ sĩ chính xuống vai khác. Thêm nghệ sĩ, đổi thứ tự hay giờ lên sân khấu thì không bất lợi — cho làm,
/// không mở hoàn. Thay ca sĩ = bỏ người cũ + thêm người mới, nên tự rơi vào loại bất lợi.
///
/// Cửa sổ hoàn dùng lại đúng khuôn của dời lịch / đổi địa điểm (<see cref="TicketRefundPolicy.FullRefundUntil"/>).
/// Trần giới hạn: nền tảng không gửi văn bản cho cơ quan nhà nước thay phòng trà, chỉ ghi lý do và nhắc.
/// </summary>
public static class LineupChange
{
    /// <summary>Độ dài tối thiểu của lý do — đủ để thành một câu, không phải một dấu chấm cho qua.</summary>
    public const int MinReasonLength = 10;
    public const int MaxReasonLength = 500;

    /// <summary>Danh sách biểu diễn sửa được khi buổi diễn còn nháp, đang chờ duyệt hoặc đã mở bán (chưa bắt đầu).</summary>
    public static void EnsureEditable(LoungeShow show)
    {
        if (show.Status is not (LoungeShowStatus.Draft or LoungeShowStatus.Pending or LoungeShowStatus.Published))
            throw new DomainException(
                "Chỉ sửa được danh sách biểu diễn khi buổi diễn chưa bắt đầu (bản nháp, chờ duyệt hoặc đang mở bán).");
    }

    /// <summary>
    /// Ghi nhận một thay đổi bất lợi. Chỉ có tác dụng với buổi ĐÃ mở bán (Published) — nháp và chờ duyệt chưa bán vé
    /// nào nên không có ai để báo. Bắt buộc lý do; đặt mốc LineupChangedAt (mở cửa sổ hoàn 100% cho người mua trước
    /// mốc này) và báo từng người đang giữ vé.
    /// </summary>
    public static async Task RecordAdverseChangeAsync(
        IUnitOfWork uow, INotificationService notifications, LoungeShow show, string performerName, string? reason,
        CancellationToken ct)
    {
        if (show.Status != LoungeShowStatus.Published) return;

        var lyDo = reason?.Trim() ?? string.Empty;
        if (lyDo.Length < MinReasonLength)
            throw new DomainException(
                $"Buổi diễn đã mở bán: phải ghi lý do thay đổi nghệ sĩ (ít nhất {MinReasonLength} ký tự). Lý do được báo " +
                "cho người đã mua vé, và NĐ 144/2020 (Điều 10 khoản 4 điểm d) yêu cầu gửi văn bản nêu rõ lý do cho cơ quan " +
                "đã chấp thuận chương trình.");
        if (lyDo.Length > MaxReasonLength) lyDo = lyDo[..MaxReasonLength];

        var changedAt = DateTimeOffset.UtcNow;
        show.LineupChangedAt = changedAt;
        show.LineupChangeNote = lyDo;
        uow.Repository<LoungeShow, Guid>().Update(show);

        var refundUntil = TicketRefundPolicy.FullRefundWindowEnd(show, changedAt);
        var tickets = await uow.Repository<Ticket, Guid>().FindAsync(
            t => t.ShowId == show.Id && t.Status == TicketStatus.Confirmed && t.BuyerId != null, ct);
        var payers = await TicketRefundRecipients.PayersAsync(uow, tickets, ct);

        foreach (var holding in tickets.GroupBy(t => t.BuyerId!.Value))
        {
            var chuyenNhuong = holding.Any(t => TicketRefundRecipients.WasTransferred(t, payers));
            await notifications.NotifyAsync(
                holding.Key,
                NotificationType.LineupChanged,
                new SongNgu("Danh sách biểu diễn đã thay đổi", "Lineup changed"),
                new SongNgu(
                    $"\"{show.Name}\" ({VietnamTime.Format(show.ScheduledStart, "HH:mm dd/MM/yyyy")}): {performerName} " +
                    $"không còn biểu diễn như đã công bố. Lý do phòng trà đưa ra: {lyDo}. " +
                    TicketRefundPolicy.DescribeFullRefundWindow(refundUntil) +
                    (chuyenNhuong ? TicketRefundRecipients.TransferredHolderCancelNote : ""),
                    $"\"{show.Name}\" ({VietnamTime.Format(show.ScheduledStart, "HH:mm dd/MM/yyyy")}): {performerName} " +
                    $"is no longer performing as announced. The venue's reason: {lyDo}. " +
                    TicketRefundPolicy.DescribeFullRefundWindowEn(refundUntil) +
                    (chuyenNhuong ? TicketRefundRecipients.TransferredHolderCancelNoteEn : "")),
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);
        }
    }
}
