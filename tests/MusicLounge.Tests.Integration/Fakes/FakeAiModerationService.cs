using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Tests.Integration.Fakes;

/// <summary>
/// MLACP-574: AI kiểm duyệt giả — điều khiển bằng DẤU trong nội dung, để test chọn được từng nhánh mà không gọi Gemini.
/// KHÔNG có dấu nào → null (AI không trả lời), đúng hành vi của môi trường không có Gemini:ApiKey mà các test cũ
/// (ModerationAiScoringTests) đang dựa vào.
/// </summary>
public sealed class FakeAiModerationService : IAiModerationService
{
    public const string DauSach = "[ai-sach]";
    public const string DauVua = "[ai-vua]";
    public const string DauRuiRoCao = "[ai-rui-ro-cao]";
    public const string DauNghiemTrong = "[ai-nghiem-trong]";

    public Task<AiModerationResult?> ScoreAsync(string content, CancellationToken ct = default)
    {
        AiModerationResult? kq =
            content.Contains(DauNghiemTrong) ? new(0.99f, "Critical", "Đe doạ một người cụ thể", "SuggestReject")
            : content.Contains(DauRuiRoCao) ? new(0.9f, "High", "Ngôn từ tục tĩu, xúc phạm", "SuggestReject")
            : content.Contains(DauVua) ? new(0.5f, "Medium", "Giọng gay gắt", "NeedsReview")
            : content.Contains(DauSach) ? new(0.03f, "Low", null, "SuggestApprove")
            : null;
        return Task.FromResult(kq);
    }
}
