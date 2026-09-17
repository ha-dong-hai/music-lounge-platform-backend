using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.LoungeShows;

/// <summary>
/// MLACP-446. Sau giờ kết thúc dự kiến bao lâu thì một buổi diễn chưa ai bấm kết thúc được coi là đã
/// xong. Một nguồn duy nhất cho ba job cùng làm việc trên một dòng thời gian:
///
/// <list type="bullet">
/// <item><c>AutoEndStaleShowsJob</c> — tự đánh dấu buổi diễn là đã kết thúc.</item>
/// <item><c>NotifyUndeliveredOfflineShowJob</c> — nhắc chủ phòng trà về buổi diễn chưa từng bắt đầu.</item>
/// <item><c>RefundUndeliveredLivestreamTicketsJob</c> — hoàn tiền vé livestream không phát.</item>
/// </list>
///
/// <para>Trước task này mỗi job có một hằng <c>DefaultGraceHours = 6</c> riêng (private). Ba con số
/// phải bằng nhau thì dòng thời gian mới nhất quán, mà không có gì bắt buộc: hạ một chỗ xuống 3 thì
/// job hoàn tiền có thể chạy trước lúc buổi diễn kịp được đánh dấu kết thúc, hoặc chủ phòng trà bị
/// nhắc về một đêm diễn mà hệ thống vẫn coi là đang diễn ra.</para>
///
/// <para>Khoảng ân hạn này chỉ để bảo vệ buổi diễn thật sự chạy dài hơn dự kiến khỏi bị đóng sớm —
/// không phải để chờ chủ phòng trà bấm nút, vì phần lớn trường hợp là họ không bao giờ bấm.
/// <c>show_auto_end_grace_hours</c> KHÔNG được seed nên con số ở đây là mốc đang áp thật.</para>
/// </summary>
public static class ShowAutoEndGrace
{
    public const int DefaultGraceHours = 6;

    public static Task<int> GraceHoursAsync(ISystemConfigService config, CancellationToken ct)
        => config.GetIntAsync(ConfigKeys.ShowAutoEndGraceHours, DefaultGraceHours, ct);
}
