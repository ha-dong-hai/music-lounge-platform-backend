using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Mutes.DTOs;

namespace MusicLounge.Application.Mutes.Queries.GetMyMutedLounges;

/// <summary>
/// Danh sách phòng trà đã tắt tiếng. Bắt buộc phải có: một lựa chọn không xem lại và không gỡ được
/// thì không phải quyền kiểm soát, mà là một cái bẫy.
/// </summary>
public sealed record GetMyMutedLoungesQuery : IQuery<IReadOnlyList<MutedLoungeDto>>;
