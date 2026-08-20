using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Performers.DTOs;

namespace MusicLounge.Application.Performers.Queries.GetPerformerById;

public sealed record GetPerformerByIdQuery(int PerformerId) : IQuery<PerformerDto>;
