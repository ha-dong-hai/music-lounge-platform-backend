using FluentValidation;

namespace MusicLounge.Application.Users.Commands.SubmitCitizenCard;

public sealed class SubmitCitizenCardCommandValidator : AbstractValidator<SubmitCitizenCardCommand>
{
    public SubmitCitizenCardCommandValidator()
    {
        RuleFor(x => x.CitizenCardNumber)
            .NotEmpty().WithMessage("Số CCCD/CMND không được để trống.")
            .Matches(@"^\d{9}$|^\d{12}$").WithMessage("Số CCCD/CMND phải gồm 9 hoặc 12 chữ số.");

        RuleFor(x => x.FrontImageUrl)
            .NotEmpty().WithMessage("Vui lòng tải ảnh mặt trước CCCD/CMND.")
            .MaximumLength(500);

        RuleFor(x => x.BackImageUrl)
            .NotEmpty().WithMessage("Vui lòng tải ảnh mặt sau CCCD/CMND.")
            .MaximumLength(500);

        // MLACP-397. So với hôm nay ở mỗi lần kiểm, không chốt một mốc ngày lúc tạo validator.
        RuleFor(x => x.DateOfBirth)
            .NotNull().WithMessage("Vui lòng nhập ngày sinh như trên CCCD/CMND.")
            .Must(d => d is null || d.Value < DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Ngày sinh phải là một ngày trong quá khứ.");
    }
}
