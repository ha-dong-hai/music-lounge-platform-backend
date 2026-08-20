using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Users.DTOs;

namespace MusicLounge.Application.Users.Queries.GetMyProfile;

public sealed record GetMyProfileQuery : IQuery<UserProfileDto>;
