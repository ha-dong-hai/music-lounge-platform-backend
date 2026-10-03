using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

// MLACP-574: AI chấm LỜI BÌNH của một đánh giá sau khi khán giả gửi. Chạy nền (không chạy trong RateShowCommandHandler)
// để AI chậm hay hỏng không làm chậm/hỏng việc gửi đánh giá — cùng lý do với ScoreModerationWithAiJob.
//
// LUẬT (chủ dự án chốt 03/10/2026): lời bình HIỆN NGAY khi gửi; AI đánh giá rủi ro High/Critical thì ẨN TẠM phần chữ
// và đưa vào hàng đợi báo cáo vi phạm của Admin; Admin quyết Gỡ hay Bỏ qua (ResolveContentReportCommandHandler).
// AI không trả lời (không có khoá, lỗi mạng, JSON hỏng) → KHÔNG làm gì: lời bình vẫn hiện, vẫn còn đường báo cáo của
// người dùng và Admin gỡ tay. AI chỉ ẩn TẠM, không bao giờ tự gỡ hẳn.
//
// TRẦN: mỗi lời bình chấm một lần (AiScore đã có thì bỏ qua); không chấm lại khi Admin đã "Bỏ qua". Ngưỡng ẩn cố định
// ở mức High — đường nâng cấp: đưa ngưỡng vào appsettings (nhóm ngưỡng phát hiện, không phải system_config) nếu thực tế
// cho thấy AI bắt nhầm/bỏ sót nhiều.
public sealed class ScoreRatingWithAiJob
{
    internal const string LyDoHeThong = "AI gắn cờ lời bình";

    private readonly ApplicationDbContext _ctx;
    private readonly IAiModerationService _aiModeration;
    private readonly ILogger<ScoreRatingWithAiJob> _logger;

    public ScoreRatingWithAiJob(
        ApplicationDbContext ctx, IAiModerationService aiModeration, ILogger<ScoreRatingWithAiJob> logger)
    {
        _ctx = ctx;
        _aiModeration = aiModeration;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(Guid ratingId, IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;

        var rating = await _ctx.Set<LoungeShowRating>().FirstOrDefaultAsync(r => r.Id == ratingId, ct);
        // Đã bị gỡ, không có chữ để chấm, hoặc đã chấm rồi (job chạy lại) — không còn gì để làm.
        if (rating is null || rating.IsRemoved || string.IsNullOrWhiteSpace(rating.Comment) || rating.AiScore is not null)
            return;

        var result = await _aiModeration.ScoreAsync(
            $"Loại nội dung: LỜI BÌNH của khán giả sau một buổi hòa nhạc ({rating.Score}/5 sao).\nLời bình: {rating.Comment}", ct);
        if (result is null)
        {
            _logger.LogInformation(
                "AI moderation unavailable for RatingId={RatingId} — comment stays visible (fail-open).", ratingId);
            return;
        }

        rating.AiScore = result.Score;
        rating.AiRiskLevel = Enum.TryParse<ModerationRiskLevel>(result.RiskLevel, true, out var risk) ? risk : null;
        rating.AiFlagReason = result.FlagReason;

        if (rating.AiRiskLevel is ModerationRiskLevel.High or ModerationRiskLevel.Critical)
        {
            var now = DateTimeOffset.UtcNow;
            rating.CommentHiddenAt = now;

            var lyDo = string.IsNullOrWhiteSpace(result.FlagReason)
                ? $"{LyDoHeThong} ({rating.AiRiskLevel})"
                : $"{LyDoHeThong} ({rating.AiRiskLevel}): {result.FlagReason}";
            _ctx.Set<ContentReport>().Add(new ContentReport
            {
                TargetType = ReportTargetType.Rating,
                TargetId = rating.Id,
                ReporterId = null, // báo cáo của hệ thống
                Reason = lyDo.Length > 500 ? lyDo[..500] : lyDo,
                Status = ContentReportStatus.Open,
                CreatedAt = now
            });

            _logger.LogWarning(
                "Rating comment hidden pending admin review: RatingId={RatingId} Risk={Risk} Score={Score}",
                ratingId, rating.AiRiskLevel, result.Score);
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
