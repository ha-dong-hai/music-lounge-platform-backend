using FluentValidation;

namespace MusicLounge.Application.Catalog.Commands.UpdateMood;

public sealed class UpdateMoodCommandValidator : AbstractValidator<UpdateMoodCommand>
{
    public UpdateMoodCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên dòng nhạc/cảm xúc không được để trống.")
            .MaximumLength(100);
    }
}
