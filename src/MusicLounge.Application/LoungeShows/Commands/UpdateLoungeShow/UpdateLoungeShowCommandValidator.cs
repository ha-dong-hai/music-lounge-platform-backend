using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.LoungeShows.Commands.UpdateLoungeShow;

public sealed class UpdateLoungeShowCommandValidator : AbstractValidator<UpdateLoungeShowCommand>
{
    public UpdateLoungeShowCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ScheduledStart).GreaterThan(DateTimeOffset.UtcNow);
        RuleFor(x => x.ScheduledEnd)
            .GreaterThan(x => x.ScheduledStart)
            .When(x => x.ScheduledEnd.HasValue);

        // Same drift as CreateLoungeShowCommandValidator was written to avoid: an invalid
        // CategoryId would otherwise only surface at SaveChangesAsync as an FK-violation
        // DbUpdateException, which GlobalExceptionHandler maps to a generic 409 with no field named.
        RuleFor(x => x.CategoryId)
            .MustAsync(async (id, ct) =>
                await uow.Repository<EventCategory, int>().AnyAsync(c => c.Id == id!.Value, ct))
            .When(x => x.CategoryId.HasValue)
            .WithMessage("CategoryId không tồn tại.");

        RuleFor(x => x.OfflineQuota).GreaterThanOrEqualTo(0).When(x => x.OfflineQuota.HasValue);
        RuleFor(x => x.OnlineQuota).GreaterThanOrEqualTo(0).When(x => x.OnlineQuota.HasValue);
    }
}
