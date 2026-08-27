using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Livestreams.DTOs;

namespace MusicLounge.Application.Livestreams.Queries.GetLivestreamCredentials;

public sealed record GetLivestreamCredentialsQuery(int LivestreamId) : IQuery<LivestreamCredentialsDto>;
