using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Constants;
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
    private readonly INotificationService _notifications;

    public DeactivateStaffCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ILogger<DeactivateStaffCommandHandler> logger,
        INotificationService notifications)
    {
        _notifications = notifications;
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

        // MLACP-381: cung ly do voi AssignStaffCommandHandler — endpoint khai bao Policies.RequireOwner
        // (cho ca Admin qua tang authorize) nhung chot tu tay o day chi so OwnerId nen Admin luon bi chan.
        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền quản lý staff cho venue này.");

        if (!assignment.IsActive)
            throw new ConflictException("Staff này đã bị vô hiệu hóa trước đó.");

        assignment.IsActive = false;
        assignment.DeactivatedAt = DateTimeOffset.UtcNow;
        assignment.DeactivatedBy = _currentUser.UserId;
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

        // MLACP-391: cung ly do voi AssignStaff — chu phong tra phai biet ai vua mat quyen soat ve/ban quay cua ho.
        var staffName = user?.FullName ?? $"tài khoản #{assignment.UserId}";
        if (_currentUser.UserId != lounge.OwnerId)
            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.VenueStaffChanged,
                "Quản trị viên đã gỡ một nhân viên khỏi phòng trà của bạn",
                $"Quản trị viên đã gỡ {staffName} khỏi danh sách nhân viên của " +
                $"\"{lounge.Name}\" — tài khoản này không còn soát vé hay bán tại quầy cho phòng trà. Nếu bạn cần biết lý " +
                "do, hãy liên hệ bộ phận hỗ trợ.",
                referenceType: "lounge",
                referenceId: lounge.Id.ToString(),
                ct: ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Staff deactivated: TargetUserId={TargetUserId} LoungeStaffId={LoungeStaffId} LoungeId={LoungeId} by OwnerId={OwnerId} at {At}",
            assignment.UserId, request.LoungeStaffId, assignment.LoungeId, _currentUser.UserId, DateTimeOffset.UtcNow);

        return Unit.Value;
    }
}
