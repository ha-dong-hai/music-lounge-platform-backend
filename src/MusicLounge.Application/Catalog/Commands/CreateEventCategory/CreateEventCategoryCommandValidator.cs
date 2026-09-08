using FluentValidation;

namespace MusicLounge.Application.Catalog.Commands.CreateEventCategory;

public sealed class CreateEventCategoryCommandValidator : AbstractValidator<CreateEventCategoryCommand>
{
    public CreateEventCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên loại buổi diễn không được để trống.")
            .MaximumLength(100);

        RuleFor(x => x.Description).MaximumLength(500);
    }
}
