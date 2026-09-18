namespace MusicLounge.Application.Moderations.DTOs;

public sealed record ContentReportQueueItemDto(
    string TargetType,
    int TargetId,
    string? TargetSummary,
    int ReportCount,
    string LatestReason,
    DateTimeOffset EarliestReportedAt,
    DateTimeOffset SlaDeadline,
    /// <summary>
    /// MLACP-456: buổi hòa nhạc mà nội dung bị báo cáo thuộc về — Show là chính nó, Livestream/ChatMessage/Rating là buổi
    /// hòa nhạc tương ứng. Có trường này thì mọi dòng trong hàng đợi mới mở được ngữ cảnh; trước đây loại Livestream không
    /// mở được vì <c>TargetId</c> là mã buổi phát chứ không phải mã buổi hòa nhạc. <c>null</c> khi không tra được.
    /// </summary>
    int? ShowId = null
);
