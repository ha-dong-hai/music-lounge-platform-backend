using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Commands.CreateLoungeShow;

public sealed class CreateLoungeShowCommandValidator : AbstractValidator<CreateLoungeShowCommand>
{
    private static readonly string[] ValidFormats = ["Offline", "Online", "Hybrid"];

    public CreateLoungeShowCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);

        RuleFor(x => x.Format)
            .Must(f => ValidFormats.Contains(f, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Format phải là 'Offline', 'Online' hoặc 'Hybrid'.");

        RuleFor(x => x.ScheduledStart)
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("Thời gian bắt đầu phải ở tương lai.");

        RuleFor(x => x.ScheduledEnd)
            .GreaterThan(x => x.ScheduledStart)
            .When(x => x.ScheduledEnd.HasValue)
            .WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");

        RuleForEach(x => x.Performances).ChildRules(p =>
        {
            p.RuleFor(x => x.PerformerId).GreaterThan(0).When(x => x.PerformerId.HasValue);
            p.RuleFor(x => x.PerformerName).MaximumLength(255).When(x => x.PerformerName is not null);
            p.RuleFor(x => x)
                .Must(x => x.PerformerId.HasValue || !string.IsNullOrWhiteSpace(x.PerformerName))
                .WithMessage("Mỗi performance phải có PerformerId hoặc PerformerName.");
            // Unguarded trước đây — handler dùng Enum.Parse<PerformerRole>(input.Role) trực tiếp,
            // client gửi sai giá trị sẽ rớt xuống 500 thay vì 400 rõ ràng như Format ở trên.
            p.RuleFor(x => x.Role)
                .Must(r => Enum.TryParse<PerformerRole>(r, ignoreCase: true, out _))
                .WithMessage($"Role phải là một trong: {string.Join(", ", Enum.GetNames<PerformerRole>())}.");
        });

        // Sai CategoryId/GenreIds truoc day roi xuong tan luc SaveChangesAsync moi vi pham FK,
        // GlobalExceptionHandler bat DbUpdateException chung chung — khong ro field nao sai.
        RuleFor(x => x.CategoryId)
            .MustAsync(async (id, ct) =>
                await uow.Repository<EventCategory, int>().AnyAsync(c => c.Id == id!.Value, ct))
            .When(x => x.CategoryId.HasValue)
            .WithMessage("CategoryId không tồn tại.");

        RuleForEach(x => x.GenreIds)
            .MustAsync(async (id, ct) => await uow.Repository<MusicGenre, int>().AnyAsync(g => g.Id == id, ct))
            .WithMessage("GenreId không tồn tại.");

        // MLACP-43 DONE WHEN: phan loai AI phai du ca 3 (the loai nhac, dong nhac, khong gian) -
        // ban local master chi co GenreIds, thieu Mood/Atmosphere.
        RuleForEach(x => x.MoodIds)
            .MustAsync(async (id, ct) => await uow.Repository<Mood, int>().AnyAsync(m => m.Id == id, ct))
            .WithMessage("MoodId không tồn tại.");

        RuleForEach(x => x.AtmosphereIds)
            .MustAsync(async (id, ct) => await uow.Repository<VenueAtmosphere, int>().AnyAsync(a => a.Id == id, ct))
            .WithMessage("AtmosphereId không tồn tại.");

        // A negative quota isn't rejected anywhere downstream — HoldTicket/SellWalkInTicket only
        // check `reserved + quantity > quota`, which a negative quota satisfies unconditionally,
        // silently blocking every sale on that channel instead of failing cleanly at creation time.
        RuleFor(x => x.OfflineQuota).GreaterThanOrEqualTo(0).When(x => x.OfflineQuota.HasValue);
        RuleFor(x => x.OnlineQuota).GreaterThanOrEqualTo(0).When(x => x.OnlineQuota.HasValue);
    }
}
