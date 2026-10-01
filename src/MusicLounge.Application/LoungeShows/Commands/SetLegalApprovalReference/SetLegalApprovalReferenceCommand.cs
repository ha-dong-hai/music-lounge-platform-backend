using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.SetLegalApprovalReference;

public sealed record SetLegalApprovalReferenceCommand(Guid ShowId, string LegalApprovalReference) : ICommand;
