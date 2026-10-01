using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Livestreams.DTOs;

namespace MusicLounge.Application.Livestreams.Queries.GetLivestreamCredentials;

public sealed record GetLivestreamCredentialsQuery(Guid LivestreamId) : IQuery<LivestreamCredentialsDto>;
