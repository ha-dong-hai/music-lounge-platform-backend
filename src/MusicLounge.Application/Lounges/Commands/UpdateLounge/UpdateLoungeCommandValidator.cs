using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.UpdateLounge;

public sealed class UpdateLoungeCommandValidator : AbstractValidator<UpdateLoungeCommand>
{
    public UpdateLoungeCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Street).NotEmpty().MaximumLength(255);
        // Cai cach hanh chinh 2025 (NQ 1171/NQ-UBTVQH15) bo cap Quan/Huyen o nhieu tinh —
        // khong con bat buoc nhap, van gioi han do dai neu co.
        RuleFor(x => x.District).MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
    }
}
