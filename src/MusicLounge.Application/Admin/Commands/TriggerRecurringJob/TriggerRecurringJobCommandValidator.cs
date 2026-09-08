using FluentValidation;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Admin.Commands.TriggerRecurringJob;

public sealed class TriggerRecurringJobCommandValidator : AbstractValidator<TriggerRecurringJobCommand>
{
    public TriggerRecurringJobCommandValidator(IBackgroundJobService jobs)
    {
        // Đối chiếu với danh sách job THẬT SỰ đang chạy, không phải một danh sách chép tay: Hangfire
        // im lặng không làm gì khi gặp id lạ, nên nếu không chặn ở đây thì Admin gõ sai một ký tự sẽ
        // nhận về 204 và tin rằng job đã chạy.
        RuleFor(x => x.JobId)
            .Must(id => jobs.GetRecurringJobIds().Contains(id))
            .WithMessage(_ =>
                $"JobId phải là một trong: {string.Join(", ", jobs.GetRecurringJobIds())}.");
    }
}
