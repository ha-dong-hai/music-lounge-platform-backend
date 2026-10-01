using FluentValidation;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Lounges.Commands.UpdateLounge;

public sealed class UpdateLoungeCommandValidator : AbstractValidator<UpdateLoungeCommand>
{
    public UpdateLoungeCommandValidator(IAdministrativeUnitCatalog catalog)
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(2000);
        // MLACP-521: luật địa chỉ dùng chung với CreateLounge.
        this.AddLoungeAddressRules(catalog);
    }
}
