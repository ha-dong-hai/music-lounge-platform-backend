using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Performers.DTOs;

namespace MusicLounge.Application.Performers.Queries.GetPerformerById;

public sealed record GetPerformerByIdQuery(Guid PerformerId) : IQuery<PerformerDto>;
