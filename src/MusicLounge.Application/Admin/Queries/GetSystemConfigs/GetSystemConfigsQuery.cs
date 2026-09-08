using MusicLounge.Application.Admin.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Admin.Queries.GetSystemConfigs;

public sealed record GetSystemConfigsQuery : IQuery<IReadOnlyList<SystemConfigDto>>;
