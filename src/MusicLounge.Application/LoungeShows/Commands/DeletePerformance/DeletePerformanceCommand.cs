using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.DeletePerformance;

// MLACP-622: ChangeReason bắt buộc khi buổi đã mở bán (báo cho người mua; NĐ 144/2020 Điều 10 khoản 4 điểm d).
public sealed record DeletePerformanceCommand(Guid PerformanceId, string? ChangeReason = null) : ICommand;
