using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using LoungeStaffEntity = MusicLounge.Domain.Entities.LoungeStaff;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Staffing.Commands.DeactivateStaff;

internal sealed class DeactivateStaffCommandHandler : IRequestHandler<DeactivateStaffCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<DeactivateStaffCommandHandler> _logger;

    public DeactivateStaffCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ILogger<DeactivateStaffCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeactivateStaffCommand request, CancellationToken ct)
    {
        var staffRepo = _uow.Repository<LoungeStaffEntity, int>();
        var assignment = await staffRepo.GetByIdAsync(request.LoungeStaffId, ct)
            ?? throw new NotFoundException(nameof(LoungeStaffEntity), request.LoungeStaffId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(assignment.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), assignment.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền quản lý staff cho venue này.");

        if (!assignment.IsActive)
            throw new ConflictException("Staff này đã bị vô hiệu hóa trước đó.");

        assignment.IsActive = false;
        assignment.DeactivatedAt = DateTimeOffset.UtcNow;
        staffRepo.Update(assignment);

        // AssignStaffCommandHandler thang cap Audience -> Staff luc gan; o day phai lam nguoc lai
        // khi khong con active o venue nao khac, neu khong user giu quyen Staff (RequireStaff
        // policy) vinh vien du da bi go khoi moi venue.
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(assignment.UserId, ct);
        if (user is not null && user.Role == UserRole.Staff)
        {
            var stillActiveElsewhere = await staffRepo.AnyAsync(
                s => s.UserId == assignment.UserId && s.IsActive && s.Id != assignment.Id, ct);
            if (!stillActiveElsewhere)
            {
                user.Role = UserRole.Audience;
                userRepo.Update(user);
            }
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Staff deactivated: TargetUserId={TargetUserId} LoungeStaffId={LoungeStaffId} LoungeId={LoungeId} by OwnerId={OwnerId} at {At}",
            assignment.UserId, request.LoungeStaffId, assignment.LoungeId, _currentUser.UserId, DateTimeOffset.UtcNow);

        return Unit.Value;
    }
}
