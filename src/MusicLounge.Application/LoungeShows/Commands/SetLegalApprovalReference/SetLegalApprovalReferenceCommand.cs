using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.SetLegalApprovalReference;

public sealed record SetLegalApprovalReferenceCommand(int ShowId, string LegalApprovalReference) : ICommand;
