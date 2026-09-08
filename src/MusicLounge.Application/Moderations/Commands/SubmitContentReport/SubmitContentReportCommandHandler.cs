using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Moderations.Commands.SubmitContentReport;

internal sealed class SubmitContentReportCommandHandler : IRequestHandler<SubmitContentReportCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SubmitContentReportCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(SubmitContentReportCommand request, CancellationToken ct)
    {
        var targetType = Enum.Parse<ReportTargetType>(request.TargetType, ignoreCase: true);

        await EnsureTargetExistsAsync(targetType, request.TargetId, ct);

        var reportRepo = _uow.Repository<ContentReport, int>();
        var alreadyReported = await reportRepo.AnyAsync(
            r => r.TargetType == targetType && r.TargetId == request.TargetId
                && r.ReporterId == _currentUser.UserId && r.Status == ContentReportStatus.Open, ct);
        if (alreadyReported)
            throw new ConflictException("Bạn đã báo cáo nội dung này rồi, đang chờ Admin xử lý.");

        var report = new ContentReport
        {
            TargetType = targetType,
            TargetId = request.TargetId,
            ReporterId = _currentUser.UserId,
            Reason = request.Reason,
            Status = ContentReportStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
        reportRepo.Add(report);
        await _uow.SaveChangesAsync(ct);

        return report.Id;
    }

    private async Task EnsureTargetExistsAsync(ReportTargetType targetType, int targetId, CancellationToken ct)
    {
        var exists = targetType switch
        {
            ReportTargetType.Show => await _uow.Repository<LoungeShow, int>().AnyAsync(s => s.Id == targetId, ct),
            ReportTargetType.Livestream => await _uow.Repository<Livestream, int>().AnyAsync(l => l.Id == targetId, ct),
            ReportTargetType.Rating => await _uow.Repository<LoungeShowRating, int>().AnyAsync(r => r.Id == targetId, ct),
            _ => throw new DomainException("TargetType không hợp lệ.")
        };
        if (!exists)
            throw new NotFoundException(targetType.ToString(), targetId);
    }
}
