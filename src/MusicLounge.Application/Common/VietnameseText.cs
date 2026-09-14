using System.Globalization;
using System.Text;

namespace MusicLounge.Application.Common;

/// <summary>
/// So khớp chữ tiếng Việt không phụ thuộc cách gõ: bỏ dấu (kể cả đ → d), không phân biệt hoa thường, mọi chuỗi ký tự không
/// phải chữ hoặc số thành một khoảng trắng. Tách nguyên văn từ <c>DonationMessageFilter</c> (MLACP-360) để MLACP-399 so tên
/// chủ tài khoản nhận tiền dùng đúng một cách chuẩn hoá, không phải hai bản gần giống nhau.
/// </summary>
public static class VietnameseText
{
    public static string Fold(string value)
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
