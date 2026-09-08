using FluentValidation;

namespace MusicLounge.Application.Catalog.Commands.CreateMusicGenre;

public sealed class CreateMusicGenreCommandValidator : AbstractValidator<CreateMusicGenreCommand>
{
    public CreateMusicGenreCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên thể loại không được để trống.")
            .MaximumLength(100);

        RuleFor(x => x.NameEn).MaximumLength(100);
    }
}
