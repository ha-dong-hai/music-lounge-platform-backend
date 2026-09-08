using FluentValidation;

namespace MusicLounge.Application.Catalog.Commands.UpdateMusicGenre;

public sealed class UpdateMusicGenreCommandValidator : AbstractValidator<UpdateMusicGenreCommand>
{
    public UpdateMusicGenreCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên thể loại không được để trống.")
            .MaximumLength(100);

        RuleFor(x => x.NameEn).MaximumLength(100);
    }
}
