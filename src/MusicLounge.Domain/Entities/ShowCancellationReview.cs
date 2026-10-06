using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

/// <summary>
/// MLACP-676 — chủ phòng trà huỷ một buổi hòa nhạc ĐÃ MỞ BÁN thì phải nêu lý do, và Admin xét lý do đó.
///
/// <para><b>Vì sao huỷ ngay rồi mới xét, không chờ duyệt mới huỷ</b> (chủ dự án chốt 06/10/2026): Admin "từ chối huỷ" không ép
/// được phòng trà biểu diễn — họ không diễn thì khán giả vẫn mất buổi, và còn mất thêm thời gian chờ. Nên buổi diễn bị huỷ và
/// khán giả được hoàn tiền NGAY; việc Admin xét chỉ quyết phòng trà có bị phạt hay không. Cùng cách Airbnb làm với chủ nhà
/// huỷ đặt phòng: khách được hoàn ngay, nền tảng xét "hoàn cảnh bất khả kháng" sau để miễn hoặc áp phí phạt.</para>
/// </summary>
public sealed class ShowCancellationReview : Common.BaseEntity<Guid>
{
    public Guid ShowId { get; set; }
    public Guid LoungeId { get; set; }
    /// <summary>Người bấm huỷ (chủ phòng trà).</summary>
    public Guid CancelledBy { get; set; }
    public ShowCancellationReason Reason { get; set; }
    public string Detail { get; set; } = string.Empty;
    /// <summary>Ảnh/tệp bằng chứng tuỳ chọn (giấy báo của cơ quan chức năng, giấy khám bệnh của nghệ sĩ…).</summary>
    public string? EvidenceUrl { get; set; }

    /// <summary>Ảnh chụp lúc huỷ: số vé đã bán qua nền tảng bị huỷ và tổng tiền phải hoàn — mức thiệt hại cho khán giả.</summary>
    public int TicketsRefunded { get; set; }
    public decimal AmountRefunded { get; set; }

    public ShowCancellationReviewStatus Status { get; set; } = ShowCancellationReviewStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset SlaDeadline { get; set; }

    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? DecisionNote { get; set; }
    /// <summary>Án phạt đã áp khi Status = Penalized.</summary>
    public Guid? PenaltyId { get; set; }

    public LoungeShow Show { get; set; } = null!;
}
