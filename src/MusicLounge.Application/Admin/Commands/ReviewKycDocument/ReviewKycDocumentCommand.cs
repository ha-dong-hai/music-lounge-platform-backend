using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Admin.Commands.ReviewKycDocument;

public enum KycDocument
{
    CitizenCard,
    TaxProfile
}

/// <param name="Approve">False rejects, and then <paramref name="Note"/> is required.</param>
/// <param name="Note">
/// Mandatory on a rejection: the submitter has to be able to act on the outcome, and "rejected" on
/// its own tells them to guess. Optional on an approval.
/// </param>
public sealed record ReviewKycDocumentCommand(
    int UserId,
    KycDocument Document,
    bool Approve,
    string? Note) : ICommand;
