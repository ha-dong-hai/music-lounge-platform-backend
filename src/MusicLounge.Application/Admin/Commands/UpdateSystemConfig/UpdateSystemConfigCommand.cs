using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Admin.Commands.UpdateSystemConfig;

/// <param name="Note">
/// Bắt buộc. SystemConfigHistory.Note được thiết kế là not-null ngay từ đầu vì đúng lý do này: một
/// dòng lịch sử ghi rằng hoa hồng đổi từ 5% sang 8% mà không nói vì sao thì gần như vô dụng khi
/// đối chiếu về sau.
/// </param>
public sealed record UpdateSystemConfigCommand(
    string ConfigKey,
    string ConfigValue,
    string Note) : ICommand;
