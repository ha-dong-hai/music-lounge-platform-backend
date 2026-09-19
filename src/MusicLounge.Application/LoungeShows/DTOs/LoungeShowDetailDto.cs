using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record LoungeShowDetailDto(
    int Id,
    string Name,
    string Description,
    string? CoverImageUrl,
    DateTimeOffset ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    LoungeShowFormat Format,
    LoungeShowStatus Status,
    bool IsOngoing,
    int? LivestreamId,
    LoungeSummaryDto Lounge,
    IReadOnlyList<PerformerSummaryDto> Performers,
    IReadOnlyList<TicketTierSummaryDto> TicketTiers,
    IReadOnlyList<GenreDto> Genres,
    IReadOnlyList<MoodDto> Moods,
    IReadOnlyList<AtmosphereDto> Atmospheres,
    RatingSummaryDto Ratings,
    IReadOnlyList<FeaturedRatingDto> FeaturedRatings,
    bool? IsWishlisted,
    bool? UserHasTicket,
    bool? UserHasRated,
    bool LegalApprovalConfirmed,
    LivestreamPlaybackMode PlaybackMode,
    // Disclosed BEFORE the buyer pays. These columns existed and were enforced by CancelTicket,
    // but appeared in no DTO at all — so an audience member decided whether to buy without being
    // able to see whether the ticket was refundable, on what terms, or by when. Both NĐ 85/2021
    // (sàn phải công khai chính sách bảo vệ người mua) and every comparable platform treat
    // publishing this as a precondition of selling, not a nice-to-have.
    TicketRefundPolicyDto RefundPolicy,
    DateTimeOffset? TicketSaleClosesAt,
    // MLACP-450: chi tra cho nguoi van hanh phong tra (chu, nhan vien duoc phan cong, Admin — dung
    // VenueOperatorAccess.CanOperate). Endpoint nay cong khai, nen ly do bi tu choi va ma VCPMC khong duoc lo
    // cho khan gia: nguoi ngoai nhan null.
    OperatorShowInfoDto? OperatorInfo = null);

/// <summary>
/// MLACP-450. Những gì người vận hành phòng trà cần biết về buổi hòa nhạc của mình mà khán giả không cần.
/// </summary>
/// <param name="Moderation">Lần kiểm duyệt gần nhất; <c>null</c> nếu buổi hòa nhạc chưa từng được gửi duyệt.</param>
/// <param name="VcpmcDeclared">D19: đã khai số tham chiếu phí tác quyền VCPMC/RIAV chưa — frontend dựa vào đây để ẩn/hiện form.</param>
/// <param name="LegalApprovalReference">
/// MLACP-461: số văn bản/đường dẫn chấp thuận của cơ quan quản lý do chính chủ phòng trà khai. Khán giả chỉ thấy cờ
/// <see cref="LoungeShowDetailDto.LegalApprovalConfirmed"/> (đã xác nhận hay chưa) — nhưng người vận hành cần đọc lại
/// CHÍNH số họ đã khai, nếu không thì sau khi khai xong không còn chỗ nào xem lại để đối chiếu hay sửa.
/// </param>
/// <param name="LegalApprovalConfirmedAt">Thời điểm Admin xác nhận văn bản hợp lệ; <c>null</c> = chưa xác nhận.</param>
public sealed record OperatorShowInfoDto(
    ShowModerationDto? Moderation,
    bool VcpmcDeclared,
    string? VcpmcRoyaltyReference,
    string? LegalApprovalReference,
    DateTimeOffset? LegalApprovalConfirmedAt);

/// <summary>
/// Trạng thái kiểm duyệt của buổi hòa nhạc, nhìn từ phía phòng trà.
///
/// <para>Cần riêng khối này vì <see cref="LoungeShowDetailDto.Status"/> không phân biệt được: bị từ chối thì buổi hòa nhạc
/// quay về <c>Draft</c>, giống hệt bản nháp chưa từng gửi, còn lý do từ chối trước đây chỉ đi qua một thông báo.</para>
/// </summary>
/// <param name="Decision"><c>null</c> = đang chờ Admin duyệt; <c>Approved</c>; <c>Rejected</c> (xem <paramref name="ReviewNote"/>).</param>
/// <param name="SlaDeadline">Hạn Admin phải duyệt xong (NĐ 147/2024) — có ý nghĩa khi đang chờ.</param>
public sealed record ShowModerationDto(
    ModerationDecision? Decision,
    string? ReviewNote,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset? SlaDeadline);

/// <param name="Summary">Ready-to-display Vietnamese sentence — built server-side so every client
/// states the same terms, and so the wording cannot drift from what CancelTicket actually enforces.</param>
public sealed record TicketRefundPolicyDto(
    bool CancellationAllowed,
    decimal RefundPercentage,
    DateTimeOffset? CancelBefore,
    int? DeadlineHoursBeforeStart,
    bool AlwaysFullRefundIfVenueCancels,
    string Summary);

// MLACP-60: "danh sach danh gia noi bat" — top danh gia co diem cao nhat va co binh luan (rating
// khong kem binh luan khong dang de hien thi thanh mot "review"). Loai danh gia da bi go (IsRemoved).
public sealed record FeaturedRatingDto(
    int Score,
    string Comment,
    string ReviewerName,
    string? ReviewerAvatarUrl,
    DateTimeOffset CreatedAt);
