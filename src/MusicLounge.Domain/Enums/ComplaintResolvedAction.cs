namespace MusicLounge.Domain.Enums;

public enum ComplaintResolvedAction
{
    Refund,
    IssueWarning,
    Dismiss,
    Compensate,

    // NĐ 147/2024/NĐ-CP: platform must be able to remove violating content upon a substantiated
    // complaint. Only valid when Complaint.TargetType == "show" — ResolveComplaintCommandHandler
    // reuses CancelLoungeShowCommand (100% refund to every confirmed ticket holder, same as an
    // owner-initiated cancel) rather than a separate takedown path, so a taken-down show gets
    // exactly the same buyer protection as any other cancellation.
    TakeDownContent
}
