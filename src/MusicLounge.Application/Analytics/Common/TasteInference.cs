namespace MusicLounge.Application.Analytics.Common;

/// <summary>
/// Dựng gu người nghe từ những buổi diễn họ đã chạm vào — vé đã mua, buổi đã lưu quan tâm, buổi vừa
/// xem. Đây là phần SUY RA, khác hẳn phần người dùng TỰ KHAI ở bước onboarding.
///
/// <b>Vì sao không lấy hợp của tất cả.</b> Cách hiển nhiên là gom mọi thẻ phân loại từng xuất hiện
/// trong lịch sử vào một tập. Cách đó hỏng theo hai hướng, và hỏng nặng dần đúng theo mức độ người
/// dùng gắn bó với sản phẩm:
///
/// <b>Một lần lệch gu nằm lại vĩnh viễn.</b> Mua vé hộ bạn một buổi EDM, hay thử một thể loại lạ
/// đúng một lần, là thể loại đó vào hồ sơ ngang hàng với thứ họ mua mười hai lần — và không có cách
/// nào rút lại.
///
/// <b>Tập gu chỉ phình ra.</b> Nó không bao giờ co lại, nên càng dùng lâu càng phủ gần hết thể loại.
/// Mẫu số Jaccard lớn dần, mọi buổi diễn chấm điểm gần bằng nhau, và cá nhân hoá âm thầm phẳng trở
/// lại thành bảng thịnh hành — trong khi lý do hiện cho người dùng vẫn khẳng định nó dựa trên những
/// buổi họ từng quan tâm. Một lời hứa không còn đúng thì tệ hơn là không hứa.
///
/// <b>Quy tắc thay thế, đọc được thành một câu:</b> một thẻ phải chiếm ít nhất một nửa số buổi diễn
/// so với thẻ mạnh nhất mới được coi là gu; dưới mức đó nó là một lần thử, không phải một sở thích.
/// Quy tắc theo tỉ lệ chứ không theo số tuyệt đối, nên nó tự đúng ở mọi cỡ lịch sử: một buổi thì thẻ
/// đó vẫn ở lại, hai thể loại ngang nhau thì giữ cả hai, mười hai chọi một thì loại cái một.
///
/// <b>Cố ý không đụng tới công thức chấm điểm.</b> <see cref="TasteMatcher"/> giữ nguyên Jaccard
/// trên tập, đúng như tài liệu đồ án ghi. Chỗ này chỉ đổi cách dựng tập đầu vào cho nó.
/// </summary>
public static class TasteInference
{
    /// <summary>
    /// Số buổi diễn gần nhất được xét. Gu người nghe đổi theo thời gian, nên một lượt quan tâm từ
    /// rất lâu trước nói được ít về việc tối nay họ muốn nghe gì. Cũng đặt trần cho khối lượng tính.
    /// </summary>
    public const int RecencyWindow = 20;

    /// <summary>
    /// Tỉ lệ tối thiểu so với thẻ mạnh nhất để một thẻ được coi là gu. Một nửa: đủ chặt để loại một
    /// lần lệch gu giữa một lịch sử rõ ràng, đủ rộng để giữ trọn một gu thật sự đa dạng.
    /// </summary>
    public const double DominanceRatio = 0.5;

    /// <summary>
    /// Gu suy ra từ danh sách buổi diễn, <b>sắp theo thứ tự mới nhất trước</b>.
    /// </summary>
    public static TasteProfile FromShows(
        IReadOnlyList<ShowTags> newestFirst, IReadOnlySet<int> followedLoungeIds)
    {
        var window = newestFirst.Take(RecencyWindow).ToList();

        return new TasteProfile(
            Dominant(window.Select(s => s.GenreIds)),
            Dominant(window.Select(s => s.MoodIds)),
            Dominant(window.Select(s => s.AtmosphereIds)),
            followedLoungeIds);
    }

    /// <summary>
    /// Những thẻ xuất hiện đủ nhiều để gọi là gu. Trả về tập rỗng khi không có thẻ nào — đó là câu
    /// trả lời đúng, không phải lỗi: buổi diễn chưa gắn thẻ thì không suy ra được gì.
    /// </summary>
    public static IReadOnlySet<int> Dominant(IEnumerable<IReadOnlySet<int>> tagsPerShow)
    {
        var appearances = new Dictionary<int, int>();

        foreach (var tags in tagsPerShow)
            foreach (var tagId in tags)
                appearances[tagId] = appearances.GetValueOrDefault(tagId) + 1;

        if (appearances.Count == 0) return new HashSet<int>();

        // Ngưỡng tính từ thẻ mạnh nhất, nên với lịch sử chỉ một buổi thì ngưỡng là 0.5 và thẻ duy
        // nhất đó (xuất hiện 1 lần) vẫn ở lại. Đây là điểm bắt buộc phải đúng: người mới là đúng
        // nhóm mà cả đường suy gu này sinh ra để phục vụ.
        var floor = appearances.Values.Max() * DominanceRatio;

        return appearances
            .Where(pair => pair.Value >= floor)
            .Select(pair => pair.Key)
            .ToHashSet();
    }
}
