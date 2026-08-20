using MediatR;
using MusicLounge.Application.Admin.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Admin.Queries.GetLedgerIntegrity;

internal sealed class GetLedgerIntegrityQueryHandler
    : IRequestHandler<GetLedgerIntegrityQuery, IReadOnlyList<LedgerIntegrityIssueDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILedgerEntryRepository _ledgerRepo;

    public GetLedgerIntegrityQueryHandler(IUnitOfWork uow, ILedgerEntryRepository ledgerRepo)
    {
        _uow = uow;
        _ledgerRepo = ledgerRepo;
    }

    public async Task<IReadOnlyList<LedgerIntegrityIssueDto>> Handle(
        GetLedgerIntegrityQuery request, CancellationToken ct)
    {
        // Debit/credit per journal is aggregated in SQL (GroupBy+Sum) instead of loading the
        // entire ever-growing, append-only ledger_entries table into app memory just to sum 2
        // columns per JournalId.
        var imbalancedJournals = await _ledgerRepo.GetImbalancedJournalsAsync(ct);
        var imbalanced = imbalancedJournals
            .Select(j => new LedgerIntegrityIssueDto("Imbalanced", j.JournalId, j.DebitTotal, j.CreditTotal));

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
        // FindAsync already applies this filter server-side — only Gateway-debit rows (a small
        // subset of the full table) are ever materialized here.
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
