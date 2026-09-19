using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.PosterJobs.Commands.CompletePosterJob;

/// <summary>
/// MLACP-458. Máy trạm nộp ảnh đã sinh xong.
/// </summary>
/// <param name="Content">Nội dung ảnh. Kiểu ảnh được suy từ CHÍNH nội dung file chứ không tin phần mở rộng do máy trạm gửi.</param>
public sealed record CompletePosterJobCommand(
    int JobId,
    string WorkerId,
    byte[] Content) : ICommand;
