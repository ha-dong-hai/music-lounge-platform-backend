using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Users.DTOs;

namespace MusicLounge.Application.Users.Queries.GetUserDetail;

public sealed record GetUserDetailQuery(int UserId) : IQuery<UserAdminDto>;
