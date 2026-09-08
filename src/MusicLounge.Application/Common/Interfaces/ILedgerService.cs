using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Interfaces;

// D8: single write point for the double-entry ledger.
// Resolves (get-or-create) ledger accounts and enforces SUM(debit) == SUM(credit) per journal.
public interface ILedgerService
{
    Task WriteJournalAsync(
        string journalId,
        string referenceType,
        string referenceId,
        int? paymentId,
        IReadOnlyList<LedgerLine> lines,
        CancellationToken ct = default);
}

public sealed record LedgerLine(
    AccountType OwnerType,
    int? OwnerId,
    decimal Amount,
    bool IsDebit,
    string? Description = null);

public static class LedgerReferenceTypes
{
    public const string Payment = "payment";
    public const string Settlement = "settlement";
    public const string Donation = "donation";
    public const string Refund = "refund";
    public const string Subscription = "subscription";
    public const string FnbOrder = "fnb_order";
}
