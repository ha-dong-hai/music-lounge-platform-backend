using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Donations;

/// <summary>
/// MLACP-363 — nhật ký bằng chứng của từng khoản donate, để khi có tranh chấp thì còn chứng minh được
/// ai đã làm gì, lúc nào, với số tiền nào.
///
/// <para>Trước đây mỗi khoản donate chỉ có vài cột trạng thái bị ghi đè qua từng bước. Không có dòng
/// thời gian, không có cách biết một cột có bị sửa về sau hay không, và ảnh chứng từ chuyển khoản chỉ là
/// một URL — file đằng sau có thể bị thay mà không để lại dấu vết.</para>
///
/// <para>Thiết kế đi theo đúng ba căn cứ Điều 11 Luật Giao dịch điện tử 2023 dùng để xác định giá trị
/// chứng cứ của thông điệp dữ liệu:</para>
/// <list type="bullet">
/// <item><b>Cách khởi tạo, lưu trữ</b>: mỗi bước là một dòng chỉ-thêm, giờ lấy từ máy chủ, ghi trong
/// cùng giao dịch với chính thay đổi nó mô tả.</item>
/// <item><b>Bảo đảm toàn vẹn</b>: mỗi dòng mang SHA-256 của chính nó và của dòng trước — sửa hay xoá một
/// dòng làm đứt chuỗi. File chứng từ upload qua hệ thống được băm nội dung ngay lúc nộp.</item>
/// <item><b>Xác định người khởi tạo</b>: mã người dùng đã đăng nhập thực hiện bước đó; null nghĩa là hệ
/// thống hoặc cổng thanh toán.</item>
/// </list>
/// </summary>
public static class DonationEvidence
{
    /// <summary>
    /// Thêm một dòng vào cuối chuỗi. Dòng được thêm vào unit of work của người gọi và lưu cùng lần
    /// <c>SaveChanges</c> với thay đổi nó mô tả. Mỗi khoản donate chỉ thêm một dòng cho mỗi lần lưu:
    /// dòng trước được đọc từ database, nên dòng thêm mà chưa lưu sẽ không được thấy.
    /// </summary>
    public static async Task AppendAsync(
        IUnitOfWork uow, int donationId, DonationEventType type, int? actorUserId,
        decimal? amount = null, string? reference = null, string? evidenceUrl = null,
        string? evidenceSha256 = null, string? detail = null, CancellationToken ct = default)
    {
        var repo = uow.Repository<DonationEvent, long>();
        var last = (await repo.FindAsync(e => e.DonationId == donationId, ct))
            .OrderByDescending(e => e.Sequence)
            .FirstOrDefault();

        var now = DateTimeOffset.UtcNow;
        var evt = new DonationEvent
        {
            DonationId = donationId,
            Sequence = (last?.Sequence ?? 0) + 1,
            EventType = type,
            // Cắt về mili-giây: giá trị đưa vào phép băm phải giống hệt giá trị đọc lại từ database.
            OccurredAt = new DateTimeOffset(now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero),
            ActorUserId = actorUserId,
            Amount = amount is { } a ? Math.Round(a, 2) : null,
            Reference = Clip(reference, 255),
            EvidenceUrl = Clip(evidenceUrl, 500),
            EvidenceSha256 = evidenceSha256,
            Detail = Clip(detail, 500),
            PreviousHash = last?.Hash
        };
        evt.Hash = ComputeHash(evt);
        repo.Add(evt);
    }

    public sealed record ChainCheck(bool IsIntact, int? FirstBrokenSequence);

    /// <summary>Kiểm lại cả chuỗi của một khoản donate (các dòng xếp theo số thứ tự).</summary>
    public static ChainCheck Verify(IReadOnlyList<DonationEvent> eventsInOrder)
    {
        string? previous = null;
        var expectedSequence = 1;
        foreach (var e in eventsInOrder)
        {
            if (e.Sequence != expectedSequence || e.PreviousHash != previous || e.Hash != ComputeHash(e))
                return new ChainCheck(false, e.Sequence);
            previous = e.Hash;
            expectedSequence++;
        }
        return new ChainCheck(true, null);
    }

    public static string Sha256Hex(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    /// <summary>
    /// Mỗi trường được mã hoá kèm độ dài ("7:abc|def") — không có dấu phân cách nào có thể bị giả mạo
    /// bằng cách nhét chính dấu đó vào nội dung một trường.
    /// </summary>
    public static string ComputeHash(DonationEvent e)
    {
        var fields = new[]
        {
            e.DonationId.ToString(CultureInfo.InvariantCulture),
            e.Sequence.ToString(CultureInfo.InvariantCulture),
            e.EventType.ToString(),
            e.OccurredAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            e.ActorUserId?.ToString(CultureInfo.InvariantCulture) ?? "",
            e.Amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? "",
            e.Reference ?? "",
            e.EvidenceUrl ?? "",
            e.EvidenceSha256 ?? "",
            e.Detail ?? "",
            e.PreviousHash ?? ""
        };
        var canonical = string.Concat(fields.Select(f => $"{f.Length}:{f}|"));
        return Sha256Hex(Encoding.UTF8.GetBytes(canonical));
    }

    private static string? Clip(string? value, int max)
        => value is null ? null : value.Length <= max ? value : value[..max];
}
