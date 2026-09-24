using FluentValidation;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Application.Users.Commands.UpdateMyLanguage;

public sealed class UpdateMyLanguageCommandValidator : AbstractValidator<UpdateMyLanguageCommand>
{
    public UpdateMyLanguageCommandValidator()
    {
        // Chặt, không chấp nhận "en-US" hay "EN": giá trị này được lưu và so sánh ở mọi nơi gửi thông báo, nên chỉ
        // có đúng hai mã. Client muốn gửi thẻ của trình duyệt thì tự rút về hai mã này.
        RuleFor(x => x.PreferredLanguage)
            .Must(ma => ma is not null && NgonNgu.HopLe.Contains(ma))
            .WithMessage("Ngôn ngữ chỉ nhận một trong hai giá trị: vi hoặc en.");
    }
}
