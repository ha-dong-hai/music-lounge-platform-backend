using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.TicketTiers.Commands.DeleteTicketTier;

public sealed record DeleteTicketTierCommand(int TierId) : ICommand;
