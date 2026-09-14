using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using LoungeStaffEntity = MusicLounge.Domain.Entities.LoungeStaff;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Staffing.Commands.AssignStaff;

internal sealed class AssignStaffCommandHandler : IRequestHandler<AssignStaffCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AssignStaffCommandHandler> _logger;
    private readonly INotificationService _notifications;

    public AssignStaffCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ILogger<AssignStaffCommandHandler> logger,
        INotificationService notifications)
    {
        _notifications = notifications;
        _uow = uow;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<int> Handle(AssignStaffCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        // MLACP-381: endpoint khai bao Policies.RequireOwner (cho ca Admin di qua tang authorize), nhung
        // chot tu tay o day chi so OwnerId nen Admin luon bi chan — cung lop loi B1 da dong o MLACP-263
        // (CreateFnbMenu). Admin can can thiep duoc (ho tro, tranh chap, chu phong tra mat quyen truy cap).
        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền quản lý staff cho venue này.");

        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        var staffRepo = _uow.Repository<LoungeStaffEntity, int>();
        var alreadyAssigned = await staffRepo.AnyAsync(
            s => s.LoungeId == request.LoungeId && s.UserId == request.UserId && s.IsActive, ct);
        if (alreadyAssigned)
            throw new ConflictException("User này đã là staff đang hoạt động của venue.");

        // Moi venue phai dung 1 tai khoan staff rieng, du cung 1 nguoi that lam viec o nhieu noi —
        // khong gom chung 1 tai khoan cho nhieu venue (tranh lan role/quyen giua cac venue doc lap).
        var activeAtOtherVenue = await staffRepo.AnyAsync(
            s => s.UserId == request.UserId && s.IsActive && s.LoungeId != request.LoungeId, ct);
        if (activeAtOtherVenue)
            throw new ConflictException(
                "Tài khoản này đang là staff đang hoạt động của venue khác. Mỗi venue cần một tài khoản staff riêng — hãy tạo tài khoản mới cho venue này.");

        if (user.Role == UserRole.Audience)
        {
            user.Role = UserRole.Staff;
            userRepo.Update(user);
        }

        var assignment = new LoungeStaffEntity
        {
            LoungeId = request.LoungeId,
            UserId = request.UserId,
            AssignedBy = _currentUser.UserId,
            IsActive = true,
            AssignedAt = DateTimeOffset.UtcNow
        };

        staffRepo.Add(assignment);

        // MLACP-391: nguoi khac chu phong tra (Admin — chot quyen o tren chi cho chu hoac Admin qua) vua trao quyen soat ve
        // va ban tai quay cho mot tai khoan — chu phong tra la nguoi chiu trach nhiem van hanh, phai biet.
        if (_currentUser.UserId != lounge.OwnerId)
            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.VenueStaffChanged,
                "Quản trị viên đã thêm nhân viên cho phòng trà của bạn",
                $"Quản trị viên đã thêm {user.FullName} ({user.Email}) làm nhân viên của \"{lounge.Name}\" — tài khoản này " +
                "có thể soát vé và bán tại quầy cho phòng trà. Nếu bạn không yêu cầu việc này, hãy gỡ nhân viên trong mục " +
                "Nhân viên và liên hệ bộ phận hỗ trợ.",
                referenceType: "lounge",
                referenceId: lounge.Id.ToString(),
                ct: ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Staff assigned: TargetUserId={TargetUserId} to LoungeId={LoungeId} by OwnerId={OwnerId} at {At}",
            request.UserId, request.LoungeId, _currentUser.UserId, DateTimeOffset.UtcNow);

        return assignment.Id;
    }
}
