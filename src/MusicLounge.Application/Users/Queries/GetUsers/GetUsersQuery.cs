using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Users.Queries.GetUsers;

public sealed record GetUsersQuery(
    string? SearchText,
    UserRole? Role,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20) : IQuery<PaginatedResult<UserAdminDto>>;
