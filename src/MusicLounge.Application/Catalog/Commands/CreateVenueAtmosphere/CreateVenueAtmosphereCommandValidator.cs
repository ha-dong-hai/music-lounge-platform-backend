using FluentValidation;

namespace MusicLounge.Application.Catalog.Commands.CreateVenueAtmosphere;

public sealed class CreateVenueAtmosphereCommandValidator : AbstractValidator<CreateVenueAtmosphereCommand>
{
    public CreateVenueAtmosphereCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên phong cách không gian không được để trống.")
            .MaximumLength(100);
    }
}
