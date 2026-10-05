using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.FnbOrders;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-679. Gọi tên một đối tượng trong câu gửi NGƯỜI ĐỌC (thông báo, lý do hoàn tiền, mô tả án phạt…) bằng thứ họ nhận ra —
/// loại án và ngày, số tiền và giờ đặt đơn, tên người mua và buổi diễn — thay cho "#&lt;mã GUID&gt;".
///
/// <para>Chủ dự án 06/10/2026: "các chỗ hiện tại show ID trên web rà soát để hiển thị thông tin". MLACP-672 đã đổi các DTO
/// sang tên; còn lại là ~40 câu thông báo/lý do do backend tự soạn, chèn thẳng mã vào chữ. Mã GUID 36 ký tự không giúp ai
/// tìm ra đối tượng — người đọc không có ô nào để dán nó vào.</para>
///
/// <para><b>Cố ý giữ mã</b>: mô tả bút toán sổ cái và OrderInfo gửi VNPay (dùng đối soát, không hiện cho người dùng — lịch
/// sử giao dịch trên web đã dịch sang tên ở MLACP-655), và mã tra cứu khiếu nại (<c>LookupReference</c>) — mã đó CHÍNH LÀ
/// cách khách vãng lai tra cứu.</para>
/// </summary>
public static class TenDoiTuong
{
    public static SongNgu LoaiAn(PenaltyType t) => t switch
    {
        PenaltyType.Warning => new("cảnh cáo", "warning"),
        PenaltyType.Suspension => new("tạm khoá", "suspension"),
        PenaltyType.Ban => new("khoá vĩnh viễn", "permanent ban"),
        _ => new(t.ToString(), t.ToString()),
    };

    /// <summary>"án cảnh cáo ngày 05/10/2026" / "the warning issued on 05/10/2026".</summary>
    public static SongNgu An(VenuePenalty p)
    {
        var loai = LoaiAn(p.PenaltyType);
        var ngay = VietnamTime.Format(p.IssuedAt, "dd/MM/yyyy");
        return new($"án {loai.Vi} ngày {ngay}", $"the {loai.En} issued on {ngay}");
    }

    /// <summary>"đơn đồ uống 120.000đ đặt lúc 20:15 05/10" — khách tự nhận ra đơn của mình qua số tiền và giờ đặt. Bản tiếng
    /// Anh không có mạo từ ("120,000 VND food &amp; drink order placed at …") để người gọi tự ghép "Your …"/"The …".</summary>
    public static SongNgu DonDoUong(FnbOrder o)
    {
        var luc = VietnamTime.Format(o.CreatedAt, "HH:mm dd/MM");
        var ban = string.IsNullOrWhiteSpace(o.TableNote) ? "" : $", {o.TableNote}";
        return new($"đơn đồ uống {VietnamMoney.Format(o.TotalAmount)} đặt lúc {luc}{ban}",
            $"{o.TotalAmount:N0} VND food & drink order placed at {luc}{ban}");
    }

    /// <summary>Viết hoa chữ cái đầu — khi tên đối tượng đứng đầu câu.</summary>
    public static string HoaDau(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    /// <summary>Khiếu nại theo mã tra cứu (mã người khiếu nại được cấp), không có thì theo ngày gửi.</summary>
    public static SongNgu KhieuNai(Complaint c)
        => !string.IsNullOrWhiteSpace(c.LookupReference)
            ? new($"khiếu nại {c.LookupReference}", $"complaint {c.LookupReference}")
            : new($"khiếu nại gửi ngày {VietnamTime.Format(c.CreatedAt, "dd/MM/yyyy")}",
                $"the complaint filed on {VietnamTime.Format(c.CreatedAt, "dd/MM/yyyy")}");

    public static SongNgu LoaiKhieuNai(ComplaintCategory c) => c switch
    {
        ComplaintCategory.EventMisrepresentation => new("buổi diễn không đúng mô tả", "show not as described"),
        ComplaintCategory.RefundDispute => new("tranh chấp hoàn tiền", "refund dispute"),
        ComplaintCategory.DonationNotPaid => new("ủng hộ chưa tới nghệ sĩ", "donation not passed on"),
        ComplaintCategory.TechnicalIssue => new("sự cố kỹ thuật", "technical issue"),
        ComplaintCategory.VenueConduct => new("cách phục vụ của phòng trà", "venue conduct"),
        ComplaintCategory.PenaltyAppeal => new("khiếu nại án phạt", "penalty appeal"),
        ComplaintCategory.ContentViolation => new("nội dung vi phạm", "content violation"),
        _ => new("khác", "other"),
    };

    /// <summary>
    /// "yêu cầu hoàn 250.000đ của Nguyễn Văn A — 2 vé "Đêm nhạc Trịnh"" (hoặc "— đơn đồ uống …"). Dùng cùng nguồn tra tên với
    /// <see cref="Refunds.RefundRequestNames"/> (MLACP-672): vé của thanh toán → buổi diễn, thanh toán F&amp;B → đơn.
    /// </summary>
    public static Task<SongNgu> YeuCauHoanAsync(IUnitOfWork uow, RefundRequest r, CancellationToken ct)
        => YeuCauHoanAsync(uow, r.PaymentId, r.AmountRequested, r.RequestedBy, ct);

    /// <summary>Bản nhận từng giá trị — cho nơi chỉ chiếu vài cột của yêu cầu hoàn (không có cả thực thể).</summary>
    public static async Task<SongNgu> YeuCauHoanAsync(
        IUnitOfWork uow, Guid paymentId, decimal amountRequested, Guid? requestedBy, CancellationToken ct)
    {
        var r = (PaymentId: paymentId, AmountRequested: amountRequested, RequestedBy: requestedBy);
        var nguoi = r.RequestedBy is Guid uid ? (await uow.Repository<User, Guid>().GetByIdAsync(uid, ct))?.FullName : null;
        var cuaAi = string.IsNullOrWhiteSpace(nguoi) ? "" : $" của {nguoi}";
        var ofWhom = string.IsNullOrWhiteSpace(nguoi) ? "" : $" from {nguoi}";

        string vi = "", en = "";
        var ves = await uow.Repository<Ticket, Guid>().FindAsync(t => t.PaymentId == r.PaymentId, ct);
        if (ves.Count > 0)
        {
            var show = await uow.Repository<LoungeShow, Guid>().GetByIdAsync(ves[0].ShowId, ct);
            if (show is not null) { vi = $" — {ves.Count} vé \"{show.Name}\""; en = $" — {ves.Count} ticket(s) for \"{show.Name}\""; }
        }
        else if (await uow.Repository<Payment, Guid>().GetByIdAsync(r.PaymentId, ct) is { } pay
                 && pay.ReferenceType == FnbOrderPayments.ReferenceType && Guid.TryParse(pay.ReferenceId, out var orderId)
                 && await uow.Repository<FnbOrder, Guid>().GetByIdAsync(orderId, ct) is { } order)
        {
            var d = DonDoUong(order);
            vi = $" — {d.Vi}"; en = $" — the {d.En}";
        }

        return new($"yêu cầu hoàn {VietnamMoney.Format(r.AmountRequested)}{cuaAi}{vi}",
            $"the {r.AmountRequested:N0} VND refund request{ofWhom}{en}");
    }
}
