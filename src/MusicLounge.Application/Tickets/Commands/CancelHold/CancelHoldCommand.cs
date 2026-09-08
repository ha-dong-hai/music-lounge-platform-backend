using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Tickets.Commands.CancelHold;

public sealed record CancelHoldCommand(int HoldId) : ICommand<bool>;
