using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

/// <summary>
/// MLACP-364 — một liên kết một lần gửi tới email của nghệ sĩ. Nghệ sĩ không có tài khoản đăng nhập;
/// liên kết là cách duy nhất để chính họ xác nhận hay phản bác điều phòng trà đã khai thay họ.
///
/// <para>Chỉ lưu bản băm của token, không lưu token: lộ bảng này cũng không dùng lại được liên kết.</para>
/// </summary>
public sealed class PerformerConfirmation : Common.BaseEntity<int>
{
    public int PerformerId { get; set; }
    public PerformerConfirmationPurpose Purpose { get; set; }

    public int? BankAccountId { get; set; }
    // Dấu vân tay thông tin tài khoản lúc gửi liên kết — tài khoản bị sửa sau đó thì liên kết cũ
    // không xác nhận được thông tin mới mà nghệ sĩ chưa từng thấy.
    public string? BankAccountFingerprint { get; set; }

    public int? DonationId { get; set; }

    public string TokenHash { get; set; } = string.Empty;
    public string SentToEmail { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
    public PerformerConfirmationOutcome? Outcome { get; set; }
    public DateTimeOffset? ConsentGivenAt { get; set; }
    public string? Note { get; set; }
}
