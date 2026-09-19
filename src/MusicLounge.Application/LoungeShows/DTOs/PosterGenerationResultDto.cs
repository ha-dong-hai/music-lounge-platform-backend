using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

/// <summary>
/// Kết quả một lần bấm "tạo poster".
///
/// MLACP-458: có hai kết cục, tuỳ nhà cung cấp đang chạy.
/// <list type="bullet">
/// <item>Gọi thẳng (Cloudflare/OpenAI): ảnh có ngay — <c>ImageUrl</c> có giá trị, <c>Status</c> là <c>Succeeded</c>.</item>
/// <item>Hàng đợi (máy trạm chạy Google Flow): mới chỉ nhận đơn — <c>ImageUrl</c> để trống, <c>Status</c> là
/// <c>Queued</c>, và <c>AttemptId</c> là mã đơn để giao diện hỏi lại trạng thái.</item>
/// </list>
/// Hai trường cuối có giá trị mặc định nên đường gọi thẳng giữ nguyên hình dạng cũ.
/// </summary>
public sealed record PosterGenerationResultDto(
    string? ImageUrl,
    int RemainingThisMonth,
    string Status = nameof(AiPosterGenerationStatus.Succeeded),
    int? AttemptId = null);
