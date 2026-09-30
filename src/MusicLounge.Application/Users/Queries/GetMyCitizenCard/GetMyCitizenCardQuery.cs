using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Queries.GetMyCitizenCard;

/// <summary>
/// Trạng thái xác minh CCCD/CMND của CHÍNH MÌNH. Trước đây chủ phòng trà nộp hồ sơ xong không có đường nào đọc lại
/// kết quả — chỉ lấy lại được ảnh (<c>GET /me/citizen-card/{side}</c>); lý do bị từ chối nằm rải trong hộp thông
/// báo. Mà danh tính đã duyệt là điều kiện để được bán (<see cref="Common.SellerIdentity"/>), nên người bán cần
/// biết mình đang ở đâu và phải làm gì. Cùng mẫu với <c>GET /me/tax-profile</c>.
/// </summary>
public sealed record GetMyCitizenCardQuery : IQuery<CitizenCardStatusDto>;

/// <param name="ReviewStatus">null = chưa nộp; Pending | Approved | Rejected.</param>
/// <param name="ReviewNote">Lý do Admin ghi khi từ chối — thứ duy nhất làm lần từ chối có thể sửa được.</param>
/// <param name="NumberMasked">Chỉ 4 số cuối: đủ để đối chiếu nhầm số, không lộ số CCCD trên màn hình.</param>
/// <param name="NumberUnreadable">MLACP-401: số đã lưu nhưng khoá mã hoá cũ đã mất — cần nộp lại.</param>
/// <param name="CanSell">Đúng luật chặn ở các cổng bán (<c>SellerIdentity.IsVerified</c>), không để máy khách tự suy.</param>
/// <param name="Explanation">Câu nói cho người bán biết trạng thái này nghĩa là gì — cùng câu các cổng bán dùng.</param>
public sealed record CitizenCardStatusDto(
    DateTimeOffset? SubmittedAt,
    string? ReviewStatus,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote,
    string? NumberMasked,
    bool NumberUnreadable,
    bool CanSell,
    string Explanation);
