using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class VenuePenalty : Common.BaseEntity<int>
{
    public int LoungeId { get; set; }
    public PenaltyType PenaltyType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceRef { get; set; }
    public int IssuedBy { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public int? SuspensionDays { get; set; }
    public DateTimeOffset? SuspensionEnd { get; set; }
    public PenaltyStatus Status { get; set; } = PenaltyStatus.Active;
    // Set by ApplyDuePenaltiesJob the moment it actually runs this penalty's venue-status change +
    // subscription compensation — the precise "has this specific penalty's consequence already
    // executed" marker. Distinct from checking the venue's CURRENT status, which two different
    // penalties of the same severity (e.g. two separate Suspensions) can both legitimately share.
    public DateTimeOffset? AppliedAt { get; set; }
    public DateTimeOffset? AppealDeadline { get; set; }
    public DateTimeOffset? AppealedAt { get; set; }
    public string? AppealReason { get; set; }
    public string? AppealResult { get; set; }   // "Overturned" / "Upheld" — nullable until resolved
    public int? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? CompensationNote { get; set; }

    // MLACP-375: an nay co cap "ngay bu mien phi" cho mot goi subscription hay khong — cong them ExpiresAt
    // ma KHONG cong gi vao AmountPaid (tam khoa duoc bu ngay, hoac khoa vinh vien duoc go gop ngay con lai
    // vao mot goi dang Active khac). Ghi lai DUNG khoang thoi gian duoc cap — khong doan vi tri — de tinh gia
    // tri con lai khi doi goi (SubscriptionTerms.RemainingValue) khong tinh nham ngay mien phi la ngay da tra
    // tien, du chu co gia han sau do lam ngay mien phi khong con nam cuoi cung.
    public int? CompensatedSubscriptionId { get; set; }
    public DateTimeOffset? SubscriptionCompensationFrom { get; set; }
    public decimal? SubscriptionCompensationDays { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
    public User IssuedByUser { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
}
