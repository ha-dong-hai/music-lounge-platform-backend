using MediatR;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Admin.Commands.TriggerRecurringJob;

internal sealed class TriggerRecurringJobCommandHandler : IRequestHandler<TriggerRecurringJobCommand, Unit>
{
    private readonly IBackgroundJobService _jobs;

    public TriggerRecurringJobCommandHandler(IBackgroundJobService jobs) => _jobs = jobs;

    public Task<Unit> Handle(TriggerRecurringJobCommand request, CancellationToken ct)
    {
        _jobs.TriggerRecurringJobNow(request.JobId);
        return Task.FromResult(Unit.Value);
    }
}
