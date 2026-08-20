using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Users.DTOs;

namespace MusicLounge.Application.Users.Queries.GetMyCitizenCardImage;

public sealed record GetMyCitizenCardImageQuery(string Side) : IQuery<CitizenCardImageDto>;
