namespace MusicLounge.Domain.Enums;

/// <summary>MLACP-676. Kết quả Admin xét lý do huỷ buổi hòa nhạc của phòng trà.</summary>
public enum ShowCancellationReviewStatus
{
    Pending,
    /// <summary>Lý do chính đáng — không phạt.</summary>
    Excused,
    /// <summary>Lý do không chính đáng — đã áp án phạt cho phòng trà.</summary>
    Penalized
}
