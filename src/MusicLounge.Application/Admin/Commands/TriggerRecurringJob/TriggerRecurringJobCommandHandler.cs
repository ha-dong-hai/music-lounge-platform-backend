using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Admin.Commands.TriggerRecurringJob;

/// <summary>
/// Chạy ngay một job định kỳ thay vì chờ tới lịch. Cần khi một job lỡ nhịp, hoặc khi vừa sửa dữ
/// liệu và muốn thấy kết quả ngay chứ không đợi tới sáng hôm sau.
/// </summary>
internal sealed class TriggerRecurringJobCommandHandler
    : IRequestHandler<TriggerRecurringJobCommand, Unit>
{
    private readonly IBackgroundJobService _jobs;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<TriggerRecurringJobCommandHandler> _logger;

    public TriggerRecurringJobCommandHandler(
        IBackgroundJobService jobs, ICurrentUserService currentUser,
        ILogger<TriggerRecurringJobCommandHandler> logger)
    {
        _jobs = jobs;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task<Unit> Handle(TriggerRecurringJobCommand request, CancellationToken ct)
    {
        // Vài job trong số này động vào tiền (giải ngân settlement, huỷ thanh toán bỏ dở). Chạy tay
        // một job như vậy là hành động vận hành có hậu quả, và không có bảng nào ghi lại việc đó.
        _logger.LogWarning(
            "Admin triggered recurring job manually: JobId={JobId} by AdminUserId={AdminUserId} at {At}",
            request.JobId, _currentUser.UserId, DateTimeOffset.UtcNow);

        _jobs.TriggerRecurringJobNow(request.JobId);
        return Task.FromResult(Unit.Value);
    }
}
