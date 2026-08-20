using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.SetVenueTourScenePosition;

public sealed class SetVenueTourScenePositionCommandValidator : AbstractValidator<SetVenueTourScenePositionCommand>
{
    public SetVenueTourScenePositionCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.SceneId).GreaterThan(0);

        // X/Y cùng null = xóa marker (chưa đặt vị trí trên bản đồ). Không cho thiếu 1 trong 2 khi
        // đặt vị trí thật, tránh lưu dữ liệu nửa-viên — cùng quy ước với SetZoneLayout3D.
        RuleFor(x => x)
            .Must(x => (x.X is null && x.Y is null) || (x.X is not null && x.Y is not null))
            .WithMessage("X/Y phải cùng có giá trị hoặc cùng để trống (xóa vị trí).");

        RuleFor(x => x.X).InclusiveBetween(0, 100).When(x => x.X is not null);
        RuleFor(x => x.Y).InclusiveBetween(0, 100).When(x => x.Y is not null);
    }
}
