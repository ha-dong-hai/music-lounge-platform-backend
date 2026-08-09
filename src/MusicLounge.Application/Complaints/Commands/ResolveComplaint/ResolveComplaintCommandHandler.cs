using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Complaints.Commands.ResolveComplaint;

internal sealed class ResolveComplaintCommandHandler : IRequestHandler<ResolveComplaintCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ILogger<ResolveComplaintCommandHandler> _logger;

    public ResolveComplaintCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        ILogger<ResolveComplaintCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<Unit> Handle(ResolveComplaintCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<Complaint, int>();
        var complaint = await repo.GetByIdAsync(request.ComplaintId, ct)
            ?? throw new NotFoundException(nameof(Complaint), request.ComplaintId);

        if (complaint.Status is ComplaintStatus.Resolved or ComplaintStatus.Rejected)
            throw new ConflictException("Khiếu nại này đã được xử lý xong.");

        var newStatus = Enum.Parse<ComplaintStatus>(request.Status, ignoreCase: true);

        complaint.Status = newStatus;
        complaint.AdminId = _currentUser.UserId;

        if (newStatus is ComplaintStatus.Resolved or ComplaintStatus.Rejected)
        {
            complaint.Resolution = request.Resolution;
            complaint.ResolvedAction = request.ResolvedAction is null
                ? null
                : Enum.Parse<ComplaintResolvedAction>(request.ResolvedAction, ignoreCase: true);
            complaint.ResolvedAt = DateTimeOffset.UtcNow;
        }

        repo.Update(complaint);

        if (complaint.ComplainantUserId is int complainantId)
        {
            await _notifications.NotifyAsync(
                complainantId,
                NotificationType.ComplaintUpdate,
                "Cập nhật khiếu nại",
                newStatus == ComplaintStatus.Resolved
                    ? "Khiếu nại của bạn đã được xử lý."
                    : newStatus == ComplaintStatus.Rejected
                        ? "Khiếu nại của bạn đã bị từ chối."
                        : "Khiếu nại của bạn đang được xem xét.",
                referenceType: "complaint",
                referenceId: complaint.Id.ToString(),
                ct: ct);
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Complaint resolved: ComplaintId={ComplaintId} NewStatus={NewStatus} ResolvedAction={ResolvedAction} by AdminUserId={AdminUserId} at {At}",
            complaint.Id, newStatus, complaint.ResolvedAction, _currentUser.UserId, DateTimeOffset.UtcNow);

        return Unit.Value;
    }
}
