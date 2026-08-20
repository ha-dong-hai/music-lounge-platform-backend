using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class LoungeShow : Common.AuditableEntity<int>
{
    public int LoungeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public DateTimeOffset ScheduledStart { get; set; }
    public DateTimeOffset? ScheduledEnd { get; set; }
    public DateTimeOffset? ActualStart { get; set; }
    public DateTimeOffset? ActualEnd { get; set; }
    public string? PosterUrl { get; set; }
    public LoungeShowFormat Format { get; set; }
    public LoungeShowStatus Status { get; set; } = LoungeShowStatus.Draft;
    public int? OfflineQuota { get; set; }
    public int? OnlineQuota { get; set; }
    public int? CategoryId { get; set; }
    public DateTimeOffset? TicketSaleClosesAt { get; set; }         // D13: khi nào đóng bán vé
    public bool CancellationAllowed { get; set; } = true;           // D13: cho phép hoàn vé không
    public int? CancellationDeadlineHours { get; set; }             // D13: hoàn trước bao nhiêu giờ
    public decimal? RefundPercentage { get; set; }                  // D13: % hoàn tiền (0-100)
    public bool IsPublic { get; set; } = true;                      // ẩn/hiện event với public
    public bool PosterByAi { get; set; } = false;                   // poster có phải AI tạo không (W02 subscription gate)
    public DateTimeOffset? RatingOpenUntil { get; set; }   // §6.13: set = actual_end + 7d when show ends

    // D18 (NĐ 144/2020/NĐ-CP Điều 8-10): show bán vé cho công chúng bắt buộc có văn bản chấp
    // thuận tổ chức biểu diễn từ Sở VHTT, nộp trước tối thiểu 7 ngày làm việc so với ScheduledStart.
    public string? LegalApprovalReference { get; set; }             // Owner khai báo số văn bản/link chấp thuận
    public int? LegalApprovalConfirmedByAdminId { get; set; }       // Admin xác nhận hợp lệ khi duyệt event
    public DateTimeOffset? LegalApprovalConfirmedAt { get; set; }

    // D19: nghĩa vụ trả phí tác quyền cho VCPMC/RIAV trước khi show DIỄN RA (khác nghĩa vụ
    // quét nội dung bản quyền qua EventModeration) — Owner khai báo, check tại lúc bắt đầu show.
    public string? VcpmcRoyaltyReference { get; set; }

    // Tour ao 3D / Online concert 3D: Owner chon phat 2D (mac dinh) hay 3D cho show Online/Hybrid.
    public LivestreamPlaybackMode PlaybackMode { get; set; } = LivestreamPlaybackMode.TwoD;

    public MusicLounge Lounge { get; set; } = null!;
    public User? LegalApprovalConfirmedByAdmin { get; set; }
    public EventCategory? Category { get; set; }
    public ICollection<Performance> Performances { get; set; } = [];
    public ICollection<TicketTier> TicketTiers { get; set; } = [];
    public ICollection<LoungeShowGenre> Genres { get; set; } = [];
    public ICollection<LoungeShowMood> Moods { get; set; } = [];
    public ICollection<LoungeShowAtmosphere> Atmospheres { get; set; } = [];
    public ICollection<LoungeShowRating> Ratings { get; set; } = [];
    public ICollection<ShowWishlist> Wishlists { get; set; } = [];
    public ICollection<UserBehaviourLog> BehaviourLogs { get; set; } = [];
    public ICollection<AiRecommendation> AiRecommendations { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = [];
    public Livestream? Livestream { get; set; }
}
