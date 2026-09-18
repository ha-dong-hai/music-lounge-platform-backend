using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.PosterJobs.Commands.FailPosterJob;

/// <summary>MLACP-458. Máy trạm báo không sinh được ảnh (Google Flow trả lỗi, hết hạn mức, mất mạng...).</summary>
public sealed record FailPosterJobCommand(
    int JobId,
    string WorkerId,
    string Reason) : ICommand;
