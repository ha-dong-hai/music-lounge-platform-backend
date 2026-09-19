using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.PosterJobs.DTOs;

namespace MusicLounge.Application.PosterJobs.Commands.ClaimPosterJob;

/// <summary>
/// MLACP-458. Máy trạm xin việc. Trả về <c>null</c> khi hàng đợi rỗng — đó là trường hợp THƯỜNG GẶP nhất, không phải lỗi.
/// </summary>
/// <param name="WorkerId">Tên máy trạm, chỉ để ghi nhật ký và gỡ lỗi khi có nhiều máy.</param>
public sealed record ClaimPosterJobCommand(string WorkerId) : ICommand<PosterJobDto?>;
