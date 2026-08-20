using MediatR;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;

namespace MusicLounge.Application.Users.Queries.GetUsers;

internal sealed class GetUsersQueryHandler
    : IRequestHandler<GetUsersQuery, PaginatedResult<UserAdminDto>>
{
    private readonly IUserRepository _userRepo;

    public GetUsersQueryHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task<PaginatedResult<UserAdminDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);

        return await _userRepo.SearchAsync(
            request.SearchText, request.Role, request.IsActive, page, size, ct);
    }
}
