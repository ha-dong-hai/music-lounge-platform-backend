using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Staffing.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using LoungeStaffEntity = MusicLounge.Domain.Entities.LoungeStaff;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Staffing.Queries.GetLoungeStaff;

internal sealed class GetLoungeStaffQueryHandler
    : IRequestHandler<GetLoungeStaffQuery, IReadOnlyList<LoungeStaffDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetLoungeStaffQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<LoungeStaffDto>> Handle(
        GetLoungeStaffQuery request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền xem danh sách staff của venue này.");

        var assignments = await _uow.Repository<LoungeStaffEntity, int>()
            .FindAsync(s => s.LoungeId == request.LoungeId, ct);

        var userIds = assignments.Select(a => a.UserId).Distinct().ToList();
        var users = await _uow.Repository<User, int>()
            .FindAsync(u => userIds.Contains(u.Id), ct);
        var usersById = users.ToDictionary(u => u.Id);

        return assignments
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new LoungeStaffDto(
                a.Id,
                a.UserId,
                usersById.TryGetValue(a.UserId, out var u) ? u.FullName : "(deleted)",
                usersById.TryGetValue(a.UserId, out var u2) ? u2.Email : "",
                a.IsActive,
                a.AssignedAt,
                a.DeactivatedAt))
            .ToList();
    }
}
