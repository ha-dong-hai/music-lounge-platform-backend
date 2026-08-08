using FluentValidation;

namespace MusicLounge.Application.Livestreams.Commands.CreateLivestream;

public sealed class CreateLivestreamCommandValidator : AbstractValidator<CreateLivestreamCommand>
{
    public CreateLivestreamCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
    }
}
