using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.VenuePenalties.Commands.ReviewAppeal;

public sealed record ReviewAppealCommand(int PenaltyId, string Decision, string? ReviewNote) : ICommand;
