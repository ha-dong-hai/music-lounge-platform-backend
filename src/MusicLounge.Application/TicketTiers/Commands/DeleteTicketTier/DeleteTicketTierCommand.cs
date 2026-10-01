using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.TicketTiers.Commands.DeleteTicketTier;

public sealed record DeleteTicketTierCommand(Guid TierId) : ICommand;
