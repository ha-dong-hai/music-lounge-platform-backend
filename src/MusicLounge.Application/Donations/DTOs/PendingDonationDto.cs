namespace MusicLounge.Application.Donations.DTOs;

public sealed record PendingDonationDto(
    int Id,
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
    DateTimeOffset? PayoutDueAt);
