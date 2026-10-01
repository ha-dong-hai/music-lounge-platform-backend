using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Staffing.DTOs;

namespace MusicLounge.Application.Staffing.Queries.GetLoungeStaff;

public sealed record GetLoungeStaffQuery(Guid LoungeId) : IQuery<IReadOnlyList<LoungeStaffDto>>;
