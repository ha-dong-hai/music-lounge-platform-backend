using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record RecommendedLoungeShowDto(
    Guid Id,
    string Name,
    string? CoverImageUrl,
    string LoungeName,
    string LoungeDistrict,
    string LoungeCity,
    // MLACP-580: ảnh đại diện phòng trà, để khối "Chương trình in riêng cho bạn" vẫn có hình khi buổi chưa có poster
    // (chủ dự án 03/10/2026: "không có hình ảnh thì thật thiếu sự trực quan"). Tách riêng khỏi CoverImageUrl chứ KHÔNG
    // gộp vào DisplayImageUrl: giao diện phải ghi rõ đây là ảnh phòng trà, không phải poster của đêm diễn.
    string? LoungeImageUrl,
    DateTimeOffset ScheduledStart,
    LoungeShowFormat Format,
    LoungeShowStatus Status,
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlyList<GenreDto> Genres,
    IReadOnlyList<string> PerformerNames,
    float RecommendationScore,
    string RecommendationReason,
    // MLACP-565: buổi này đến từ đâu — để giao diện gắn nhãn "AI gợi ý" CHỈ trên thẻ thật sự do AI chọn (chủ dự án
    // 03/10/2026: nền tảng nổi bật nhờ AI; nhưng gắn nhãn AI cho bảng thịnh hành là nói sai với người dùng).
    //   "Ai"       — kết quả job nền tính sẵn: ML.NET (nội dung + lọc cộng tác + tuỳ chỉnh), lý do có thể do Gemini viết.
    //               Chỉ có với người đã đăng nhập VÀ đã bật đồng ý AI.
    //   "Taste"    — khớp gu, tính ngay trong request: sở thích tự khai / phòng trà theo dõi / buổi khách vừa xem.
    //   "Trending" — không khớp gì, xếp theo vé bán + lượt lưu gần đây.
    string RecommendationSource);
