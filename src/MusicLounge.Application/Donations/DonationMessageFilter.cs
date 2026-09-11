using System.Globalization;
using System.Text;
using System.Text.Json;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Donations;

/// <summary>
/// MLACP-360 — quyết định lời nhắn của một donate có được lên livestream hay không.
///
/// <para>Trước đây cảnh báo donate chỉ phát khi chủ phòng trà bấm "đã nhận", nên cú bấm đó vô tình
/// là bước duyệt lời nhắn. Nay cảnh báo phát ngay khi VNPay xác nhận, nên cần lớp lọc tự động — cùng
/// cách YouTube làm với Super Chat: kiểm duyệt như tin nhắn live chat thường, chủ kênh đặt danh sách
/// từ cấm và gỡ được tin đã hiện.</para>
///
/// <para>Chỉ lời nhắn bị giữ lại; bản thân donate (tên hoặc "Ẩn danh", số tiền) vẫn được xướng lên.
/// Tiền là thật và đã được VNPay xác nhận, nội dung chữ mới là thứ cần kiểm.</para>
/// </summary>
public static class DonationMessageFilter
{
    /// <summary>Đọc danh sách từ cấm (mảng JSON các chuỗi). False khi không đọc được.</summary>
    public static bool TryParseList(string raw, out IReadOnlyList<string> normalizedWords)
    {
        normalizedWords = [];
        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(raw);
            if (parsed is null) return false;
            normalizedWords = parsed
                .Where(w => w is not null)
                .Select(Normalize)
                .Where(w => w.Length > 0)
                .Distinct()
                .ToList();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// So khớp theo TỪ trên văn bản đã chuẩn hoá: "cam" không chặn "camera". Bỏ dấu và hoa thường
    /// để "Từ Khóa" và "tu khoa" bị bắt như nhau — gõ không dấu là cách lách phổ biến nhất.
    /// </summary>
    public static bool ContainsBlocked(string message, IReadOnlyList<string> normalizedWords)
    {
        if (normalizedWords.Count == 0) return false;
        var text = " " + Normalize(message) + " ";
        return normalizedWords.Any(w => text.Contains(" " + w + " ", StringComparison.Ordinal));
    }

    /// <summary>Lời nhắn được phép lên sóng, hoặc null.</summary>
    public static async Task<string?> MessageForBroadcastAsync(
        Donation donation, ISystemConfigService config, CancellationToken ct)
        => AllowedMessage(donation.Message, donation.IsMessagePublic, donation.MessageHiddenAt,
            await LoadBlockedWordsAsync(config, ct));

    /// <summary>Danh sách từ cấm đã chuẩn hoá; null khi danh sách trong cấu hình bị hỏng.</summary>
    public static async Task<IReadOnlyList<string>?> LoadBlockedWordsAsync(ISystemConfigService config, CancellationToken ct)
    {
        var raw = await config.GetStringAsync(ConfigKeys.DonationMessageBlockedWords, "[]", ct);
        return TryParseList(raw, out var words) ? words : null;
    }

    /// <summary>
    /// Lời nhắn được phép hiện công khai, hoặc null. MLACP-365: livestream và trang sao kê công khai
    /// dùng chung quy tắc này — lời nhắn đã bị gỡ khỏi sóng không được hiện lại ở trang công khai.
    /// </summary>
    public static string? AllowedMessage(
        string? message, bool isMessagePublic, DateTimeOffset? hiddenAt, IReadOnlyList<string>? blockedWords)
    {
        if (!isMessagePublic || hiddenAt is not null || string.IsNullOrWhiteSpace(message))
            return null;

        // Đường Admin đã kiểm định dạng lúc ghi, nên danh sách hỏng ở đây chỉ có thể do sửa tay trong
        // database. Khi đó giữ lời nhắn lại: chặn nhầm thì người donate vẫn thấy lời nhắn trong lịch
        // sử của mình, còn phát nhầm một câu xúc phạm lên sóng thì không rút lại được.
        if (blockedWords is null) return null;

        return ContainsBlocked(message, blockedWords) ? null : message;
    }

    internal static string Normalize(string value)
    {
        var lowered = value.ToLowerInvariant().Replace('đ', 'd');
        var decomposed = lowered.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }
        return sb.ToString().Trim();
    }
}
