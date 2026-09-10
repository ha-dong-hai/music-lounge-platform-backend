using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common;

/// <summary>
/// Hệ thống biết được gì về việc buổi diễn có thật sự diễn ra hay không.
/// </summary>
public enum ShowCompletionVerdict
{
    /// <summary>
    /// Chưa đóng, nên chưa có gì để nói. <b>Thiếu bằng chứng</b> — cố ý khác với
    /// <see cref="NeverStarted"/>, vốn là bằng chứng thật.
    /// </summary>
    Unknown,

    /// <summary>
    /// Đã đóng lại mà chưa từng được đánh dấu bắt đầu.
    ///
    /// <para>Cặp giá trị này mang đúng một nghĩa, không mơ hồ: <c>EndLoungeShow</c> bắt buộc trạng
    /// thái <c>Ongoing</c>, mà <c>Ongoing</c> chỉ đến từ <c>StartLoungeShow</c> hoặc
    /// <c>StartLivestream</c> — hai chỗ duy nhất trong toàn bộ mã nguồn ghi <c>ActualStart</c>. Nên
    /// mọi buổi diễn do <b>người</b> kết thúc đều có <c>ActualStart</c>. Chỉ ba đường sinh ra cặp
    /// này: job tự đóng buổi diễn quá hạn, webhook kết thúc livestream, và kiểm duyệt gỡ nội dung —
    /// cả ba đều nghĩa là buổi diễn không giao được thứ đã bán.</para>
    /// </summary>
    NeverStarted,

    /// <summary>Có đủ hai mốc, đo được tỉ lệ thời lượng.</summary>
    Measured
}

/// <param name="Ratio">Chỉ có giá trị khi <see cref="ShowCompletionVerdict.Measured"/>.</param>
public readonly record struct ShowCompletionEvidence(
    ShowCompletionVerdict Verdict,
    TimeSpan? ScheduledDuration,
    TimeSpan? ActualDuration,
    decimal? Ratio);

/// <summary>
/// D16: thời lượng thật chia thời lượng dự kiến, dùng để quyết tranche có tự giải ngân hay phải chờ
/// Admin xem.
///
/// <para>Tách ra khỏi <c>SettlementReleaseJob</c> ở MLACP-335 vì màn hình Admin phải hiển thị
/// <b>đúng con số đã giữ khoản đó lại</b>. Một quy tắc có hai bản sao thì sớm muộn cũng lệch.</para>
///
/// <para><b>MLACP-336 tách <see cref="ShowCompletionVerdict.NeverStarted"/> ra khỏi
/// <see cref="ShowCompletionVerdict.Unknown"/>.</b> Trước đó cả hai gộp làm một và đều được coi là
/// đạt. Chú thích tại chỗ quyết định ghi lý do: *"no tracking mechanism wired up yet"* — mệnh đề đó
/// đúng vào thời điểm nó được viết, khi một buổi diễn không ai đóng thì cả hai mốc đều null. Nhưng
/// từ khi có job tự đóng buổi diễn quá hạn, xuất hiện một cặp thứ ba mà đoạn code cũ không lường
/// tới: đã đóng, chưa từng bắt đầu. Đó là bằng chứng của việc không diễn ra, không phải thiếu bằng
/// chứng — và hai thứ đó phải được xử lý khác nhau.</para>
/// </summary>
public static class ShowCompletion
{
    public static ShowCompletionEvidence Evaluate(LoungeShow? show)
    {
        if (show?.ActualEnd is null)
            return new ShowCompletionEvidence(ShowCompletionVerdict.Unknown, null, null, null);

        if (show.ActualStart is null)
            return new ShowCompletionEvidence(ShowCompletionVerdict.NeverStarted, null, null, null);

        var scheduledDuration = ShowSchedule.EffectiveEnd(show) - show.ScheduledStart;
        var actualDuration = show.ActualEnd.Value - show.ActualStart.Value;

        // Lịch dài 0 hoặc âm là dữ liệu hỏng, không phải bằng chứng buổi diễn kém — chia cho nó chỉ
        // ra một con số vô nghĩa.
        if (scheduledDuration <= TimeSpan.Zero)
            return new ShowCompletionEvidence(
                ShowCompletionVerdict.Unknown, scheduledDuration, actualDuration, null);

        return new ShowCompletionEvidence(
            ShowCompletionVerdict.Measured,
            scheduledDuration,
            actualDuration,
            (decimal)(actualDuration / scheduledDuration));
    }

    /// <summary>
    /// Buổi diễn có chắc chắn đã không giao được thứ đã bán hay không.
    ///
    /// <para>Tách riêng khỏi <see cref="IsAcceptable"/> vì hai chốt này áp ở hai phạm vi khác nhau —
    /// xem chú thích tại chỗ gọi trong <c>SettlementReleaseJob</c>.</para>
    /// </summary>
    public static bool IsDefinitelyUndelivered(ShowCompletionEvidence evidence)
        => evidence.Verdict == ShowCompletionVerdict.NeverStarted;

    /// <summary>
    /// Không kết luận được thì coi là đạt — chặn mọi khoản chi trả cuối cùng chỉ vì thiếu dữ liệu sẽ
    /// giam tiền của mọi phòng trà làm ăn tử tế. Nhưng "chưa từng bắt đầu" <b>không</b> phải là
    /// thiếu dữ liệu.
    /// </summary>
    public static bool IsAcceptable(ShowCompletionEvidence evidence, decimal threshold)
        => evidence.Verdict switch
        {
            ShowCompletionVerdict.Unknown => true,
            ShowCompletionVerdict.NeverStarted => false,
            _ => evidence.Ratio!.Value >= threshold
        };
}
