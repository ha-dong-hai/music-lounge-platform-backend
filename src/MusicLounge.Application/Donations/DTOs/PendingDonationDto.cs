namespace MusicLounge.Application.Donations.DTOs;

public sealed record PendingDonationDto(
    Guid Id,
    string PerformerName,
    string ShowName,
    decimal Gross,
    decimal Net,
    decimal AmountToPayPerformer,
    bool IsAnonymous,
    string? DisplayName,
    string? Message,
    DateTimeOffset? PaymentConfirmedAt,
    DateTimeOffset? AutoConfirmDeadline,
    // MLACP-362: luc nen tang da chuyen tien cho phong tra (null: chua chuyen), va han chuyen tiep
    // cho nghe si — cung mot moc voi nhac nho, canh cao va dieu kien khieu nai.
    DateTimeOffset? PayoutReceivedAt,
    DateTimeOffset? PayoutDueAt,
    // MLACP-644: nghệ sĩ nhận khoản này, và nghệ sĩ đã có tài khoản nhận tiền MẶC ĐỊNH chưa — đúng điều kiện
    // ConfirmDonationPaidCommandHandler từ chối (422). Trước đây chủ phòng trà chỉ biết SAU KHI đã chuyển tiền và gõ mã
    // giao dịch (đo 05/10/2026). PerformerHasPayoutAccount chỉ được tính ở danh sách "chờ chuyển cho nghệ sĩ"
    // (GetOwnerReceivedDonationsQueryHandler); danh sách chờ xác nhận để false.
    Guid? PerformerId,
    bool PerformerHasPayoutAccount);
