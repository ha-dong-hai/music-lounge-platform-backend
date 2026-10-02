using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.VenuePenalties.Commands.SubmitAppeal;

public sealed record SubmitAppealCommand(Guid PenaltyId, string AppealReason) : ICommand;
