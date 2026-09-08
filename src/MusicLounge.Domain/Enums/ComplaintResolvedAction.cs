namespace MusicLounge.Domain.Enums;

public enum ComplaintResolvedAction
{
    Refund,
    IssueWarning,
    Dismiss,

    // "Compensate" da bi bo (MLACP-286). No khong bao gio lam gi ca, va ba ly do doc lap deu dan
    // toi ket luan khong nen xay:
    //   1. Eventbrite — nen tang ban ve su kien cung mo hinh, van hanh that — khong co hanh dong
    //      tuong duong: chi co hoan tien bat buoc va tranh chap qua ngan hang
    //   2. Khong co kenh chi tra nao cho khan gia. BankAccountOwnerType chi co Lounge va Performer;
    //      ngoai viec dao lai chinh giao dich cu, he thong khong co cach nao chuyen tien toi mot
    //      nguoi mua ve
    //   3. Hinh thuc kha di duy nhat — credit vao vi — la dung thu StubHub lam, va khi ho dua credit
    //      THAY CHO tien mat thi bi Tong chuong ly California xu ly, hoan hon 20 trieu USD
    // Giu mot gia tri enum khong lam gi TE HON la bo no: no van lo ra API, Admin van chon duoc, he
    // thong van tra 204 va van gui "khieu nai da duoc xu ly" trong khi khong co gi xay ra.

    // NĐ 147/2024/NĐ-CP: platform must be able to remove violating content upon a substantiated
    // complaint. Only valid when Complaint.TargetType == "show" — ResolveComplaintCommandHandler
    // reuses CancelLoungeShowCommand (100% refund to every confirmed ticket holder, same as an
    // owner-initiated cancel) rather than a separate takedown path, so a taken-down show gets
    // exactly the same buyer protection as any other cancellation.
    TakeDownContent
}
