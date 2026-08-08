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
    }
}
