using FluentValidation;

namespace MusicLounge.Application.Catalog.Commands.CreateMood;

public sealed class CreateMoodCommandValidator : AbstractValidator<CreateMoodCommand>
{
    public CreateMoodCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên dòng nhạc/cảm xúc không được để trống.")
            .MaximumLength(100);
    }
}
