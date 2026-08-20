using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Lounges.DTOs;

namespace MusicLounge.Application.Lounges.Queries.GetLoungeDetail;

public sealed record GetLoungeDetailQuery(int LoungeId) : IQuery<LoungeDetailDto>;
