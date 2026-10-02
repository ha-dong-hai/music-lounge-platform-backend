using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.VenuePenalties.Commands.ReviewAppeal;

public sealed record ReviewAppealCommand(Guid PenaltyId, string Decision, string? ReviewNote) : ICommand;
