using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common;

/// <summary>
/// Bằng chứng về việc một buổi diễn có chạy đủ thời lượng đã hứa hay không (D16).
/// </summary>
/// <param name="CanJudge">
/// Có đủ dữ liệu để kết luận hay không. <c>false</c> nghĩa là <b>không biết</b> — cố ý khác với
/// "biết là không đạt", vì hai thứ đó dẫn tới hai cách xử lý khác nhau.
/// </param>
public readonly record struct ShowCompletionEvidence(
    bool CanJudge,
    TimeSpan? ScheduledDuration,
    TimeSpan? ActualDuration,
    decimal? Ratio);

/// <summary>
/// D16: thời lượng thật chia thời lượng dự kiến, dùng để quyết tranche cuối có tự giải ngân hay
/// phải chờ Admin xem.
///
/// <para>Tách ra khỏi <c>SettlementReleaseJob</c> ở MLACP-335 vì màn hình Admin phải hiển thị
/// <b>đúng con số đã park khoản đó</b>. Nếu query tự tính lại bằng một bản sao công thức thì hai
/// bên sẽ lệch nhau lúc nào không hay — đúng loại lỗi đã xảy ra nhiều lần trong codebase này khi
/// một quy tắc có hai định nghĩa.</para>
/// </summary>
public static class ShowCompletion
{
    public static ShowCompletionEvidence Evaluate(LoungeShow? show)
    {
        if (show?.ActualStart is null || show.ActualEnd is null)
            return new ShowCompletionEvidence(CanJudge: false, null, null, null);

        var scheduledDuration = ShowSchedule.EffectiveEnd(show) - show.ScheduledStart;
        var actualDuration = show.ActualEnd.Value - show.ActualStart.Value;

        // Lịch dài 0 hoặc âm là dữ liệu hỏng, không phải bằng chứng buổi diễn kém — chia cho nó chỉ
        // ra một con số vô nghĩa.
        if (scheduledDuration <= TimeSpan.Zero)
            return new ShowCompletionEvidence(CanJudge: false, scheduledDuration, actualDuration, null);

        return new ShowCompletionEvidence(
            CanJudge: true,
            scheduledDuration,
            actualDuration,
            (decimal)(actualDuration / scheduledDuration));
    }

    /// <summary>
    /// Không kết luận được thì coi là đạt — chặn mọi khoản chi trả cuối cùng chỉ vì thiếu dữ liệu
    /// sẽ giam tiền của mọi phòng trà làm ăn tử tế.
    /// </summary>
    public static bool IsAcceptable(ShowCompletionEvidence evidence, decimal threshold)
        => !evidence.CanJudge || evidence.Ratio!.Value >= threshold;
}
