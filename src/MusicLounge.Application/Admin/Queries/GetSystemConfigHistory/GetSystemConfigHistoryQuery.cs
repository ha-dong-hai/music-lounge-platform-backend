using MusicLounge.Application.Admin.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Admin.Queries.GetSystemConfigHistory;

public sealed record GetSystemConfigHistoryQuery(string ConfigKey)
    : IQuery<IReadOnlyList<SystemConfigHistoryDto>>;
