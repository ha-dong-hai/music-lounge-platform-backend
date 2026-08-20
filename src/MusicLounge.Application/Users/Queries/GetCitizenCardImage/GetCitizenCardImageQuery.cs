using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Users.DTOs;

namespace MusicLounge.Application.Users.Queries.GetCitizenCardImage;

public sealed record GetCitizenCardImageQuery(int TargetUserId, string Side) : IQuery<CitizenCardImageDto>;
