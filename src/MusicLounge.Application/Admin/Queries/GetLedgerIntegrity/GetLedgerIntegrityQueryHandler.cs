using MediatR;
using MusicLounge.Application.Admin.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Admin.Queries.GetLedgerIntegrity;

internal sealed class GetLedgerIntegrityQueryHandler
    : IRequestHandler<GetLedgerIntegrityQuery, IReadOnlyList<LedgerIntegrityIssueDto>>
{
    private readonly IUnitOfWork _uow;

    public GetLedgerIntegrityQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<LedgerIntegrityIssueDto>> Handle(
        GetLedgerIntegrityQuery request, CancellationToken ct)
    {
        var entries = await _uow.Repository<LedgerEntry, int>().GetAllAsync(ct);

        var imbalanced = entries
            .GroupBy(e => e.JournalId)
            .Select(g => new LedgerIntegrityIssueDto(
                "Imbalanced",
                g.Key,
                g.Where(e => e.IsDebit).Sum(e => e.Amount),
                g.Where(e => !e.IsDebit).Sum(e => e.Amount)))
            .Where(x => x.DebitTotal != x.CreditTotal);

        // A duplicated VNPay callback that slips past the process-local idempotency lock (bug
        // class fixed separately in ProcessDonationPaymentCommandHandler/ProcessSubscriptionPaymentCommandHandler/
        // ProcessVnPayCallbackCommandHandler) writes 2 SEPARATE, individually-balanced journals for
        // the same confirm event instead of 1 — invisible to the debit==credit check above, since
        // each journal balances fine on its own.
        //
        // The signature of "money just arrived from VNPay for this reference" is a debit line on
        // AccountType.Gateway — written exactly once per confirm event by WriteTicketLedgerHandler,
        // ProcessSubscriptionPaymentCommandHandler, and ProcessDonationPaymentCommandHandler (chặng
        // 1). Deliberately NOT grouping by PaymentId alone: SettlementReleaseJob legitimately writes
        // further journals against the SAME PaymentId later (one per settlement tranche, e.g.
        // First70/Final30 — D3/§6.6), and donation chặng 2 (ConfirmDonationPaidCommandHandler)
        // legitimately writes a second journal against the SAME donation ReferenceId — neither of
        // those carries a Gateway-debit line, so filtering to just that line excludes both
        // legitimate later stages and only catches the confirm step actually firing twice.
        var gatewayDebits = await _uow.Repository<LedgerEntry, int>().FindAsync(
            e => e.Account.OwnerType == AccountType.Gateway && e.IsDebit, ct);

        var duplicateConfirmJournals = gatewayDebits
            .GroupBy(e => e.PaymentId.HasValue ? $"payment:{e.PaymentId}" : $"{e.ReferenceType}:{e.ReferenceId}")
            .Where(g => g.Select(e => e.JournalId).Distinct().Count() > 1)
            .Select(g => new LedgerIntegrityIssueDto(
                "DuplicateConfirmJournal",
                string.Join(",", g.Select(e => e.JournalId).Distinct()),
                g.Sum(e => e.Amount),
                g.Sum(e => e.Amount),
                $"{g.Key} has {g.Select(e => e.JournalId).Distinct().Count()} separate confirm journals"));

        return imbalanced
            .Concat(duplicateConfirmJournals)
            .ToList();
    }
}
