using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Users.Queries.GetUsers;

/// <param name="CreatedFrom">MLACP-598: chỉ lấy tài khoản đăng ký từ thời điểm này (gồm cả). Bỏ trống = không chặn dưới.</param>
/// <param name="CreatedTo">MLACP-598: chỉ lấy tài khoản đăng ký tới thời điểm này (gồm cả). Bỏ trống = không chặn trên.</param>
public sealed record GetUsersQuery(
    string? SearchText,
    UserRole? Role,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null) : IQuery<PaginatedResult<UserAdminDto>>;
