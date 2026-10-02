using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeModel3D;

public sealed class SetLoungeModel3DCommandValidator : AbstractValidator<SetLoungeModel3DCommand>
{
    public SetLoungeModel3DCommandValidator()
    {
        RuleFor(x => x.LoungeId).NotEmpty();
        RuleFor(x => x.ModelUrl).MaximumLength(500);
    }
}
