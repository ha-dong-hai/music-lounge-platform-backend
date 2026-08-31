using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.VenuePenalties.Commands.SubmitAppeal;

public sealed record SubmitAppealCommand(int PenaltyId, string AppealReason) : ICommand;
