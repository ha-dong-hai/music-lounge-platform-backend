using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Lounges.Commands.CreateLounge;

public sealed class CreateLoungeCommandValidator : AbstractValidator<CreateLoungeCommand>
{
    public CreateLoungeCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Street).NotEmpty().MaximumLength(255);
        // Cai cach hanh chinh 2025 (NQ 1171/NQ-UBTVQH15) bo cap Quan/Huyen o nhieu tinh —
        // khong con bat buoc nhap, van gioi han do dai neu co.
        RuleFor(x => x.District).MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);

        // Truoc day AtmosphereId sai (vd 0, hoac ID khong ton tai) roi den tan luc SaveChangesAsync
        // moi vi pham FK constraint, GlobalExceptionHandler bat DbUpdateException chung chung roi
        // tra "Du lieu da ton tai hoac xung dot" — khong noi ro la field nao sai. Check som o day de
        // FE nhan duoc 400 ro rang dung field.
        RuleFor(x => x.AtmosphereId)
            .MustAsync(async (id, ct) =>
                await uow.Repository<VenueAtmosphere, int>().AnyAsync(a => a.Id == id!.Value, ct))
            .When(x => x.AtmosphereId.HasValue)
            .WithMessage("AtmosphereId không tồn tại.");
    }
}
