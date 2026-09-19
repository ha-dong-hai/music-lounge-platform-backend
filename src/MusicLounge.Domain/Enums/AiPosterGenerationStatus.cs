namespace MusicLounge.Domain.Enums;

/// <summary>
/// MLACP-458: ba trạng thái chờ được thêm cho chế độ hàng đợi (nhà cung cấp là máy trạm chạy Google Flow, xem
/// <c>PosterQueue</c>). Cột lưu dạng CHUỖI (<c>HasConversion&lt;string&gt;</c>) nên thêm giá trị mới không đụng tới cấu
/// trúc cột, và mọi dòng cũ vẫn đọc đúng như trước. Thứ tự khai báo giữ nguyên Succeeded/Failed ở đầu để không ai
/// vô tình phụ thuộc vào giá trị số nếu có chỗ nào ép kiểu.
/// </summary>
public enum AiPosterGenerationStatus
{
    Succeeded,
    Failed,

    /// <summary>Đơn đã nhận, đang đợi máy trạm đến lấy. Có tính vào hạn mức tháng (giữ chỗ) — xem GeneratePosterCommandHandler.</summary>
    Queued,

    /// <summary>Một máy trạm đã nhận đơn và đang sinh ảnh. Quá <c>LeaseExpiresAt</c> mà chưa xong thì đơn được trả về hàng đợi.</summary>
    Rendering,

    /// <summary>
    /// Đơn nằm chờ quá lâu vì không có máy trạm nào trực. Khác <see cref="Failed"/> ở nguyên nhân: không phải nhà cung cấp
    /// trả lỗi, mà là không ai đến nhận việc. Cũng không trừ hạn mức của chủ phòng trà.
    /// </summary>
    Expired
}
