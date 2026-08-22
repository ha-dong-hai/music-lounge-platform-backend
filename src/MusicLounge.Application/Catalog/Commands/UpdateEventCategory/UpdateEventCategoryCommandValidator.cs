using FluentValidation;

namespace MusicLounge.Application.Catalog.Commands.UpdateEventCategory;

public sealed class UpdateEventCategoryCommandValidator : AbstractValidator<UpdateEventCategoryCommand>
{
    public UpdateEventCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên loại sự kiện không được để trống.")
            .MaximumLength(100);

        RuleFor(x => x.Description).MaximumLength(500);
    }
}
