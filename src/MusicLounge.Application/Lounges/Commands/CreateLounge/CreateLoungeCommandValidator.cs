using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Application.Lounges;

namespace MusicLounge.Application.Lounges.Commands.CreateLounge;

public sealed class CreateLoungeCommandValidator : AbstractValidator<CreateLoungeCommand>
{
    public CreateLoungeCommandValidator(IUnitOfWork uow, IAdministrativeUnitCatalog catalog)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(2000);
        // MLACP-521: luật địa chỉ dùng chung với UpdateLounge.
        this.AddLoungeAddressRules(catalog);

        // Truoc day AtmosphereId sai (vd 0, hoac ID khong ton tai) roi den tan luc SaveChangesAsync
        // moi vi pham FK constraint, GlobalExceptionHandler bat DbUpdateException chung chung roi
        // tra "Du lieu da ton tai hoac xung dot" — khong noi ro la field nao sai. Check som o day de
        // FE nhan duoc 400 ro rang dung field.
        RuleFor(x => x.AtmosphereId)
            .MustAsync(async (id, ct) =>
                await uow.Repository<VenueAtmosphere, int>().AnyAsync(a => a.Id == id!.Value, ct))
            .When(x => x.AtmosphereId.HasValue)
            .WithMessage("AtmosphereId không tồn tại.");
    }
}
