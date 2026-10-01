using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Users.DTOs;

namespace MusicLounge.Application.Users.Queries.GetCitizenCardImage;

public sealed record GetCitizenCardImageQuery(Guid TargetUserId, string Side) : IQuery<CitizenCardImageDto>;
