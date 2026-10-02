using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.VenuePenalties.Commands.IssuePenalty;

public sealed record IssuePenaltyCommand(
    Guid LoungeId,
    string PenaltyType,
    string Reason,
    string? EvidenceRef,
    int? SuspensionDays) : ICommand<Guid>;
