using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.VenuePenalties.Commands.IssuePenalty;

public sealed record IssuePenaltyCommand(
    int LoungeId,
    string PenaltyType,
    string Reason,
    string? EvidenceRef,
    int? SuspensionDays) : ICommand<int>;
