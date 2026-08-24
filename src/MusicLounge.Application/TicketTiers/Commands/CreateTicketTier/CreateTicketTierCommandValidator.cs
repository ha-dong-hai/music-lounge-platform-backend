using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.TicketTiers.Commands.CreateTicketTier;

public sealed class CreateTicketTierCommandValidator : AbstractValidator<CreateTicketTierCommand>
{
    private static readonly string[] ValidAccessTypes = ["Physical", "Livestream"];
    private static readonly string[] ValidChannels = ["Online", "Offline", "Both"];

    public CreateTicketTierCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);

        // Sai ZoneId truoc day roi xuong tan luc SaveChangesAsync moi vi pham FK, GlobalExceptionHandler
        // bat DbUpdateException chung chung — khong ro field nao sai.
        RuleFor(x => x.ZoneId)
            .MustAsync(async (id, ct) =>
                await uow.Repository<SeatingZone, int>().AnyAsync(z => z.Id == id!.Value, ct))
            .When(x => x.ZoneId.HasValue)
            .WithMessage("ZoneId không tồn tại.");

        RuleFor(x => x.AccessType)
            .Must(a => ValidAccessTypes.Contains(a, StringComparer.OrdinalIgnoreCase))
            .WithMessage("AccessType phải là 'Physical' hoặc 'Livestream'.");

        // Khac Quota (moi Price rieng) o duoi da co GreaterThan(0) — TotalCapacity (cap toan tier)
        // truoc day khong co rule nao, Owner co the set 0/am ma khong bi chan o day.
        RuleFor(x => x.TotalCapacity)
            .GreaterThan(0)
            .When(x => x.TotalCapacity.HasValue)
            .WithMessage("TotalCapacity phải lớn hơn 0.");

        RuleFor(x => x.Prices).NotEmpty().WithMessage("Phải có ít nhất 1 đợt giá vé.");

        RuleForEach(x => x.Prices).ChildRules(p =>
        {
            p.RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
            p.RuleFor(x => x.Price).GreaterThan(0);
            p.RuleFor(x => x.Quota).GreaterThan(0).When(x => x.Quota.HasValue);
            p.RuleFor(x => x.PurchaseChannel)
                .Must(c => ValidChannels.Contains(c, StringComparer.OrdinalIgnoreCase))
                .WithMessage("PurchaseChannel phải là 'Online', 'Offline' hoặc 'Both'.");
            p.RuleFor(x => x.SaleEnd).GreaterThan(x => x.SaleStart);
        });
    }
}
