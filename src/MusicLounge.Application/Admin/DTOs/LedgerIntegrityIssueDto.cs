namespace MusicLounge.Application.Admin.DTOs;

public sealed record LedgerIntegrityIssueDto(
    string IssueType,
    string JournalId,
    decimal DebitTotal,
    decimal CreditTotal,
    string? Detail = null);
