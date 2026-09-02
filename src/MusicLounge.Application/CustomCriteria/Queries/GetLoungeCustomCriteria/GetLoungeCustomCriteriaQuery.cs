using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.CustomCriteria.DTOs;

namespace MusicLounge.Application.CustomCriteria.Queries.GetLoungeCustomCriteria;

public sealed record GetLoungeCustomCriteriaQuery(int LoungeId) : IQuery<IReadOnlyList<CustomCriteriaDto>>;
