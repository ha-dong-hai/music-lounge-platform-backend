namespace MusicLounge.Application.LoungeShows.Commands.GeneratePoster;

/// <summary>
/// MLACP-422: cau lenh mo ta gui cho model tao anh.
///
/// Truoc day prompt dua ca TEN CHUONG TRINH, DIA DIEM va NGAY DIEN vao anh. Poster that dau tien sinh tren Azure
/// (16/09, show "Đêm nhạc Trịnh – Hạ trắng") cho ra chu "Sêêm nhûc Trỹnh", "Shậi triâng" va ngay gio la day so vo
/// nghia — model khuech tan khong viet duoc tieng Viet co dau, do la gioi han chung chu khong rieng nha cung cap
/// mien phi.
///
/// Nay chi sinh NEN KHONG CHU, chua khoang trong de giao dien chen tieu de that len tren. Anh vi vay cung dung lai
/// duoc khi doi gio dien va khong bao gio hien sai ten chuong trinh.
/// </summary>
public static class PosterPrompt
{
    /// <summary>Cau chan chu — viet bang tieng Anh vi model bam sat huong dan tieng Anh hon.</summary>
    public const string NoTextRule =
        "Absolutely no text, no words, no letters, no numbers, no typography, no watermark, no logo.";

    public static string Build(string loungeName, IReadOnlyCollection<string> tags, string? styleHint)
    {
        var tagLine = tags.Count > 0 ? string.Join(", ", tags) : "nhạc sống";

        var prompt =
            "Background artwork for a live music poster at a Vietnamese tea-house music venue "
            + $"(\"phòng trà\") named \"{loungeName}\". "
            + $"Mood and musical style keywords: {tagLine}. "
            + "Warm stage lighting, intimate indoor atmosphere, acoustic instruments, cinematic depth, "
            + "square composition with clean empty space in the upper third for a title to be added later. "
            + NoTextRule;

        if (!string.IsNullOrWhiteSpace(styleHint))
            prompt += $" Extra request from the venue owner: {styleHint}.";

        return prompt;
    }
}
