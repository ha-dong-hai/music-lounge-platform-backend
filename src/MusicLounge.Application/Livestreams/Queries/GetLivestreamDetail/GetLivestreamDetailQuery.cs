using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Livestreams.DTOs;

namespace MusicLounge.Application.Livestreams.Queries.GetLivestreamDetail;

public sealed record GetLivestreamDetailQuery(int LivestreamId) : IQuery<LivestreamDetailDto>;
