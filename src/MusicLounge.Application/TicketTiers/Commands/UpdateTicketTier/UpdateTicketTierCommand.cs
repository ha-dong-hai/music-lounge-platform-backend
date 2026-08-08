using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.TicketTiers.Commands.UpdateTicketTier;

public sealed record UpdateTicketTierCommand(
    int TierId,
    string Name,
    string? Description,
    int? TotalCapacity
) : ICommand;
