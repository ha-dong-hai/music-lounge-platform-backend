using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.SetVcpmcRoyaltyReference;

public sealed record SetVcpmcRoyaltyReferenceCommand(int ShowId, string VcpmcRoyaltyReference) : ICommand;
