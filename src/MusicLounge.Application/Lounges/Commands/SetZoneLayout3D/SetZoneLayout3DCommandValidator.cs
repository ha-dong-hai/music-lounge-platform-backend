using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.SetZoneLayout3D;

public sealed class SetZoneLayout3DCommandValidator : AbstractValidator<SetZoneLayout3DCommand>
{
    public SetZoneLayout3DCommandValidator()
    {
        RuleFor(x => x.ZoneId).GreaterThan(0);

        // X/Y/Z ca 3 cung null = xoa marker (chua gan vi tri 3D). Khong cho thieu 1 trong 3 khi
        // dat vi tri that, tranh luu du lieu nua-vien.
        RuleFor(x => x)
            .Must(x => (x.X is null && x.Y is null && x.Z is null) ||
                       (x.X is not null && x.Y is not null && x.Z is not null))
            .WithMessage("X/Y/Z phải cùng có giá trị hoặc cùng để trống (xóa vị trí).");
    }
}
