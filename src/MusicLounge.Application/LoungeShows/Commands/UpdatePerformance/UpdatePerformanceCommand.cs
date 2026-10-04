using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.UpdatePerformance;

public sealed record UpdatePerformanceCommand(
    Guid PerformanceId,
    string Role,
    int OrderIndex,
    TimeOnly? SetTime,
    bool AcceptsDonation,
    // MLACP-622: bắt buộc khi buổi đã mở bán và thay đổi là hạ nghệ sĩ chính xuống vai khác.
    string? ChangeReason = null
) : ICommand;
