using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Moderations.Commands.ResolveContentReport;

public sealed record ResolveContentReportCommand(
    string TargetType,
    int TargetId,
    string Action,
    string? Note
) : ICommand;
