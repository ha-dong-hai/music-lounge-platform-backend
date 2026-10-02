using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.SetVcpmcRoyaltyReference;

public sealed record SetVcpmcRoyaltyReferenceCommand(Guid ShowId, string VcpmcRoyaltyReference) : ICommand;
