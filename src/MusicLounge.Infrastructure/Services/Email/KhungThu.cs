using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Infrastructure.Services.Email;

/// <summary>Một nút hành động chính của thư (mỗi thư tối đa một — luật "một việc chính mỗi thư").</summary>
internal sealed record NutThu(string Chu, string Url);

/// <summary>
/// MLACP-635. Nội dung một thư, chưa định dạng. <see cref="KhungThu"/> dựng nó thành HAI phần cùng nội dung: HTML và chữ
/// trơn. Mỗi loại thư chỉ khai nội dung; cách trình bày nằm ở một chỗ, để 16 loại thư sau này không thành 16 kiểu.
/// </summary>
internal sealed record ThuEmail
{
    public required string NgonNgu { get; init; }
    /// <summary>Câu lớn đầu thư — nói KẾT QUẢ hoặc VIỆC CẦN LÀM (kim tự tháp ngược).</summary>
    public required string TieuDe { get; init; }
    /// <summary>Dòng xem trước hộp thư hiện cạnh tiêu đề (40–130 ký tự). Bổ sung cho tiêu đề, không lặp lại.</summary>
    public required string XemTruoc { get; init; }
    public required string TenNguoiNhan { get; init; }
    public IReadOnlyList<string> DoanMo { get; init; } = [];
    /// <summary>Mã in cỡ lớn (mã xác thực). Thư bảo mật thì đây là thứ duy nhất trong thân thư.</summary>
    public string? MaLon { get; init; }
    public IReadOnlyList<(string Nhan, string GiaTri)> Bang { get; init; } = [];
    public NutThu? Nut { get; init; }
    public IReadOnlyList<string> DoanSau { get; init; } = [];
    /// <summary>Vì sao người này nhận thư — in ở chân thư (người nhận có quyền biết, và thư giao dịch rõ lý do ít bị coi là rác).</summary>
    public required string LyDoNhan { get; init; }
}

/// <summary>
/// MLACP-635. Khung thư chung. Chuẩn áp dụng (nguồn: reports/Email - bộ mẫu thư gửi người dùng (bản đề xuất 05-10-2026).md):
/// khung rộng tối đa 600px dựng bằng bảng, CSS viết ngay trong thẻ (Gmail/Outlook bỏ thẻ &lt;style&gt;), phông hệ thống,
/// thân 16px, một nút chính, liên kết dự phòng dạng chữ dưới nút, luôn kèm bản chữ trơn.
/// Màu cùng bảng màu trang khán giả (sơn then + lụa ngà). Không ảnh: nhiều hộp thư chặn ảnh mặc định, thư phải đọc được khi
/// không có ảnh nào.
/// Mọi chuỗi đưa vào HTML đều qua bộ mã hoá HTML (E) — tên buổi diễn, tên người do người dùng tự gõ.
/// </summary>
internal static class KhungThu
{
    private const string Nen = "#F3EEE4", The = "#FFFDF8", Muc = "#1F1A17", ChuThan = "#3B342E", ChuNhat = "#6B625A", Ke = "#E6DED0";
    private const string Phong = "Arial,Helvetica,sans-serif";

    public static (string Html, string ChuTron) Dung(ThuEmail t)
    {
        var chan = ChanThu(t);
        return (Html(t, chan), ChuTron(t, chan));
    }

    private static IReadOnlyList<string> ChanThu(ThuEmail t) =>
    [
        t.LyDoNhan,
        new SongNgu("MusicLounge — nền tảng đặt vé phòng trà ca nhạc.", "MusicLounge — tickets for live music lounges.").Theo(t.NgonNgu),
        new SongNgu("Thư gửi tự động, vui lòng không trả lời thư này.", "This is an automated email. Please do not reply.").Theo(t.NgonNgu),
    ];

    private static string LoiChao(ThuEmail t) =>
        new SongNgu($"Xin chào {t.TenNguoiNhan},", $"Hello {t.TenNguoiNhan},").Theo(t.NgonNgu);

    // Chỉ thoát ký tự đặc biệt của HTML (< > & " ').  WebUtility.HtmlEncode còn biến chữ có dấu thành &#225;… — hộp thư
    // vẫn hiện đúng nhưng thư phình to và khó đọc khi soát (test đo được 05/10).
    private static readonly HtmlEncoder MaHoa = HtmlEncoder.Create(UnicodeRanges.All);
    private static string E(string s) => MaHoa.Encode(s);

    private static string Html(ThuEmail t, IReadOnlyList<string> chan)
    {
        var b = new StringBuilder();
        b.Append($"<!DOCTYPE html><html lang=\"{(t.NgonNgu == "en" ? "en" : "vi")}\"><head><meta charset=\"utf-8\">");
        b.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>").Append(E(t.TieuDe)).Append("</title></head>");
        b.Append($"<body style=\"margin:0;padding:0;background-color:{Nen};\">");
        // Dòng xem trước: ẩn khỏi thân thư nhưng hộp thư vẫn đọc làm đoạn xem trước.
        b.Append($"<div style=\"display:none;max-height:0;overflow:hidden;mso-hide:all;\">{E(t.XemTruoc)}</div>");
        b.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color:{Nen};\"><tr><td align=\"center\" style=\"padding-top:24px;padding-bottom:24px;padding-left:12px;padding-right:12px;\">");
        b.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;background-color:{The};border:1px solid {Ke};\">");

        // Đầu thư
        b.Append($"<tr><td style=\"background-color:{Muc};padding-top:18px;padding-bottom:18px;padding-left:32px;padding-right:32px;font-family:{Phong};font-size:20px;font-weight:bold;color:{Nen};\">MusicLounge</td></tr>");

        b.Append($"<tr><td style=\"padding-top:32px;padding-bottom:8px;padding-left:32px;padding-right:32px;font-family:{Phong};\">");
        b.Append($"<h1 style=\"margin-top:0;margin-bottom:20px;font-size:24px;line-height:32px;font-weight:bold;color:{Muc};\">{E(t.TieuDe)}</h1>");
        DoanHtml(b, LoiChao(t));
        foreach (var d in t.DoanMo) DoanHtml(b, d);

        if (t.MaLon is not null)
            b.Append($"<p style=\"margin-top:8px;margin-bottom:24px;font-family:'Courier New',Courier,monospace;font-size:34px;line-height:44px;font-weight:bold;letter-spacing:8px;color:{Muc};\">{E(t.MaLon)}</p>");

        if (t.Bang.Count > 0)
        {
            b.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin-top:8px;margin-bottom:24px;border-top:1px solid {Ke};\">");
            foreach (var (nhan, giaTri) in t.Bang)
                b.Append($"<tr><td valign=\"top\" style=\"width:38%;padding-top:10px;padding-bottom:10px;padding-right:12px;border-bottom:1px solid {Ke};font-family:{Phong};font-size:14px;line-height:20px;color:{ChuNhat};\">{E(nhan)}</td>")
                 .Append($"<td valign=\"top\" style=\"padding-top:10px;padding-bottom:10px;border-bottom:1px solid {Ke};font-family:{Phong};font-size:16px;line-height:22px;color:{Muc};\">{E(giaTri)}</td></tr>");
            b.Append("</table>");
        }

        if (t.Nut is { } nut)
        {
            var url = E(nut.Url);
            b.Append($"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin-top:8px;margin-bottom:16px;\"><tr><td style=\"background-color:{Muc};\">")
             .Append($"<a href=\"{url}\" style=\"display:inline-block;padding-top:14px;padding-bottom:14px;padding-left:28px;padding-right:28px;font-family:{Phong};font-size:16px;font-weight:bold;color:#FFFFFF;text-decoration:none;\">{E(nut.Chu)}</a>")
             .Append("</td></tr></table>");
            var duPhong = new SongNgu("Nút không bấm được? Dán liên kết này vào trình duyệt:", "Button not working? Paste this link into your browser:").Theo(t.NgonNgu);
            b.Append($"<p style=\"margin-top:0;margin-bottom:24px;font-size:13px;line-height:20px;color:{ChuNhat};word-break:break-all;\">{E(duPhong)}<br><a href=\"{url}\" style=\"color:{ChuNhat};\">{url}</a></p>");
        }

        foreach (var d in t.DoanSau) DoanHtml(b, d);
        b.Append("</td></tr>");

        // Chân thư
        b.Append($"<tr><td style=\"padding-top:20px;padding-bottom:24px;padding-left:32px;padding-right:32px;border-top:1px solid {Ke};font-family:{Phong};font-size:13px;line-height:20px;color:{ChuNhat};\">");
        b.Append(string.Join("<br>", chan.Select(E)));
        b.Append("</td></tr></table></td></tr></table></body></html>");
        return b.ToString();
    }

    private static void DoanHtml(StringBuilder b, string doan) =>
        b.Append($"<p style=\"margin-top:0;margin-bottom:16px;font-size:16px;line-height:24px;color:{ChuThan};\">{E(doan)}</p>");

    private static string ChuTron(ThuEmail t, IReadOnlyList<string> chan)
    {
        var b = new StringBuilder();
        b.AppendLine(t.TieuDe).AppendLine();
        b.AppendLine(LoiChao(t)).AppendLine();
        foreach (var d in t.DoanMo) b.AppendLine(d).AppendLine();
        if (t.MaLon is not null) b.AppendLine("    " + t.MaLon).AppendLine();
        if (t.Bang.Count > 0)
        {
            foreach (var (nhan, giaTri) in t.Bang) b.AppendLine($"{nhan}: {giaTri}");
            b.AppendLine();
        }
        if (t.Nut is { } nut) b.AppendLine($"{nut.Chu}: {nut.Url}").AppendLine();
        foreach (var d in t.DoanSau) b.AppendLine(d).AppendLine();
        b.AppendLine("—");
        foreach (var c in chan) b.AppendLine(c);
        return b.ToString();
    }
}
