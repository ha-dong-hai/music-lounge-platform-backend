using FluentValidation;

namespace MusicLounge.Application.CustomCriteria.Commands.UpdateCustomCriteria;

internal sealed class UpdateCustomCriteriaCommandValidator : AbstractValidator<UpdateCustomCriteriaCommand>
{
    public UpdateCustomCriteriaCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        // Cùng giới hạn với lệnh tạo: tên dài hơn cột thì lỗi hiện ra ở tầng cơ sở dữ liệu, khó đọc.
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
