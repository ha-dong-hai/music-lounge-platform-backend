namespace MusicLounge.Application.Analytics.Common;

/// <summary>
/// Gu của một người nghe, quy về dạng chấm điểm được.
///
/// Nguồn của nó khác nhau tuỳ người đang hỏi, và sự khác nhau đó là điểm mấu chốt về quyền riêng tư:
///
/// <b>Người đã đăng nhập</b> — lấy từ sở thích họ TỰ KHAI ở bước onboarding (thể loại/tâm trạng/không
/// gian) cộng với phòng trà họ đang theo dõi. Đây là dữ liệu người dùng chủ động đưa cho hệ thống,
/// không phải thứ hệ thống suy ra từ việc theo dõi hành vi.
///
/// <b>Khách chưa đăng nhập</b> — lấy từ chính nội dung request họ vừa gửi (buổi diễn họ vừa xem,
/// thể loại họ vừa chọn). Không lưu lại, không gắn với định danh nào, không đặt cookie. Nó chỉ tồn
/// tại đúng trong thời gian xử lý một request để sắp xếp câu trả lời cho request đó.
/// </summary>
public sealed record TasteProfile(
    IReadOnlySet<int> GenreIds,
    IReadOnlySet<int> MoodIds,
    IReadOnlySet<int> AtmosphereIds,
    IReadOnlySet<int> FollowedLoungeIds)
{
    public static TasteProfile Empty { get; } =
        new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    /// <summary>
    /// Không biết gì về người này. Khi đó phải trả về bảng đang được quan tâm y như cũ — sắp xếp
    /// theo một cái gu rỗng chỉ tạo ra thứ tự ngẫu nhiên đội lốt cá nhân hoá.
    /// </summary>
    public bool KnowsNothing
        => GenreIds.Count == 0 && MoodIds.Count == 0
           && AtmosphereIds.Count == 0 && FollowedLoungeIds.Count == 0;
}

/// <summary>Các thẻ phân loại của một buổi diễn, đủ để so với gu người nghe.</summary>
public sealed record ShowTags(
    int ShowId,
    int LoungeId,
    IReadOnlySet<int> GenreIds,
    IReadOnlySet<int> MoodIds,
    IReadOnlySet<int> AtmosphereIds);

/// <summary>
/// Chấm điểm một buổi diễn hợp gu người nghe tới đâu.
///
/// Công thức <c>genre*0.4 + mood*0.4 + atmosphere*0.2</c>, mỗi chiều là Jaccard giữa tập thẻ của
/// buổi diễn và tập sở thích của người nghe, cộng thêm thưởng nếu người đó đang theo dõi phòng trà.
/// Đây là công thức được ghi trong tài liệu đồ án, không phải chi tiết cài đặt tự do đổi.
///
/// <b>Vì sao tách ra thành lớp riêng.</b> Trước đây công thức này nằm chôn bên trong
/// <c>MLNetRecommendationService</c>, chỉ chạy được trong job nền chạy mỗi 6 tiếng. Giờ có thêm
/// đường tính trực tiếp trong request (cho khách chưa đăng nhập, và cho người dùng chưa bật đồng ý
/// AI), nên nếu để hai bản cài đặt riêng thì sớm muộn chúng cho ra hai kết quả khác nhau cho cùng
/// một người — đúng lớp lỗi mà codebase này đã gặp nhiều lần. Một công thức, một chỗ.
/// </summary>
public static class TasteMatcher
{
    public const float GenreWeight = 0.4f;
    public const float MoodWeight = 0.4f;
    public const float AtmosphereWeight = 0.2f;

    /// <summary>
    /// Cộng thêm sau khi đã chấm xong điểm hợp gu, không gộp vào công thức. Theo dõi một phòng trà
    /// là hành động dứt khoát của người dùng và đáng được tính riêng — DICE cũng tách "nghệ sĩ/địa
    /// điểm bạn theo dõi" thành một tín hiệu độc lập với việc khớp gu.
    /// </summary>
    public const float FollowedVenueBoost = 0.15f;

    /// <summary>Điểm hợp gu thuần tuý, chưa cộng thưởng theo dõi.</summary>
    public static float ContentScore(TasteProfile taste, ShowTags show)
        => Jaccard(taste.GenreIds, show.GenreIds) * GenreWeight
           + Jaccard(taste.MoodIds, show.MoodIds) * MoodWeight
           + Jaccard(taste.AtmosphereIds, show.AtmosphereIds) * AtmosphereWeight;

    /// <summary>Điểm dùng để xếp hạng: hợp gu cộng thưởng nếu đang theo dõi phòng trà đó.</summary>
    public static float RankingScore(TasteProfile taste, ShowTags show)
        => ContentScore(taste, show)
           + (taste.FollowedLoungeIds.Contains(show.LoungeId) ? FollowedVenueBoost : 0f);

    /// <summary>
    /// Giao trên hợp. Một trong hai tập rỗng thì bằng 0 — không biết gu người nghe, hoặc buổi diễn
    /// chưa gắn thẻ nào, thì không có căn cứ để nói là hợp.
    /// </summary>
    public static float Jaccard(IReadOnlySet<int> a, IReadOnlySet<int> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0f;

        var intersection = a.Count(b.Contains);
        var union = a.Count + b.Count - intersection;

        return union == 0 ? 0f : (float)intersection / union;
    }
}
