using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Moderations.Commands.SubmitContentReport;

public sealed record SubmitContentReportCommand(
    string TargetType,
    int TargetId,
    string Reason
) : ICommand<int>;
