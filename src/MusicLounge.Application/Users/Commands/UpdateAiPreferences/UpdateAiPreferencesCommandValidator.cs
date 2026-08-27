using FluentValidation;

namespace MusicLounge.Application.Users.Commands.UpdateAiPreferences;

public sealed class UpdateAiPreferencesCommandValidator : AbstractValidator<UpdateAiPreferencesCommand>
{
    public UpdateAiPreferencesCommandValidator()
    {
        RuleFor(x => x.GenreIds)
            .NotNull()
            .Must(ids => ids.Count <= 10)
            .WithMessage("Tối đa 10 thể loại nhạc.");

        RuleFor(x => x.MoodIds)
            .NotNull()
            .Must(ids => ids.Count <= 10)
            .WithMessage("Tối đa 10 tâm trạng.");

        RuleFor(x => x.AtmosphereIds)
            .NotNull()
            .Must(ids => ids.Count <= 10)
            .WithMessage("Tối đa 10 không khí.");
    }
}
