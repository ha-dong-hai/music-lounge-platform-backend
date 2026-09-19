using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// Every AI poster generation attempt for a show — a log, not just the current poster. Exists so
// (a) SubscriptionPackage.MaxAiPostersPerMonth can be enforced accurately (only Succeeded rows
// count against an Owner's monthly quota — a Failed attempt is the vendor's fault, not theirs, so
// it must never cost them a poster), and (b) there's an auditable record to point to if an Owner
// disputes "I paid for posters I never got" — the log shows exactly which attempts failed and why,
// distinct from ones that succeeded but the Owner simply regenerated over.
public sealed class AiPosterGeneration : Common.BaseEntity<int>
{
    public int ShowId { get; set; }
    public int OwnerId { get; set; }
    public AiPosterGenerationStatus Status { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // MLACP-458: các cột dưới đây chỉ có nghĩa ở chế độ hàng đợi (nhà cung cấp là máy trạm chạy Google Flow). Ở chế độ
    // gọi thẳng (Cloudflare/OpenAI) chúng luôn để trống, vì dòng nhật ký được ghi sau khi đã có ảnh — không có giai đoạn
    // chờ nào để mà ghi lại.
    /// <summary>Máy trạm nào đang giữ đơn này. Để trống nghĩa là chưa ai nhận.</summary>
    public string? ClaimedBy { get; set; }

    public DateTimeOffset? ClaimedAt { get; set; }

    /// <summary>
    /// Hạn chót của lượt nhận việc. Quá mốc này mà đơn vẫn <c>Rendering</c> thì coi như máy trạm đã chết giữa chừng và
    /// đơn được trả về hàng đợi — không có cách nào khác để biết, vì máy trạm nằm ngoài tầm với của máy chủ.
    /// </summary>
    public DateTimeOffset? LeaseExpiresAt { get; set; }

    /// <summary>Đã giao cho máy trạm mấy lần. Chặn vòng lặp vô hạn khi một lời nhắc luôn làm máy trạm chết.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Nhà cung cấp đã (hoặc sẽ) sinh ảnh này: <c>flow</c>, <c>cloudflare</c>, <c>openai</c>. Để đối chiếu về sau.</summary>
    public string? Provider { get; set; }

    public LoungeShow Show { get; set; } = null!;
    public User Owner { get; set; } = null!;
}
