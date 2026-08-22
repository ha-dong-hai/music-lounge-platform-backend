using FluentValidation;

namespace MusicLounge.Application.Catalog.Commands.UpdateVenueAtmosphere;

public sealed class UpdateVenueAtmosphereCommandValidator : AbstractValidator<UpdateVenueAtmosphereCommand>
{
    public UpdateVenueAtmosphereCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên phong cách không gian không được để trống.")
            .MaximumLength(100);
    }
}
