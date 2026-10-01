using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Moderations.Commands.SubmitContentReport;

public sealed record SubmitContentReportCommand(
    string TargetType,
    Guid TargetId,
    string Reason
) : ICommand<Guid>;
