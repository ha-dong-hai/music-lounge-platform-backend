using System.Text.RegularExpressions;
using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Users.Commands.SubmitTaxProfile;

public sealed partial class SubmitTaxProfileCommandValidator : AbstractValidator<SubmitTaxProfileCommand>
{
    // Mã số thuế Việt Nam: 10 chữ số, hoặc 10 + "-" + 3 chữ số cho đơn vị trực thuộc.
    [GeneratedRegex(@"^\d{10}(-\d{3})?$")]
    private static partial Regex TaxCodePattern();

    public SubmitTaxProfileCommandValidator()
    {
        RuleFor(x => x.BusinessType)
            .Must(t => Enum.TryParse<PayeeBusinessType>(t, ignoreCase: true, out _))
            .WithMessage($"Loại hình kinh doanh phải là một trong: {string.Join(", ", Enum.GetNames<PayeeBusinessType>())}.");

        RuleFor(x => x.TaxCode)
            .NotEmpty().WithMessage("Mã số thuế không được để trống.")
            .Must(c => TaxCodePattern().IsMatch(c.Trim()))
            .WithMessage("Mã số thuế phải gồm 10 chữ số, hoặc 10 chữ số kèm 3 chữ số đơn vị trực thuộc (ví dụ 0123456789-001).");

        RuleFor(x => x.LegalName)
            .NotEmpty()
            .When(x => Enum.TryParse<PayeeBusinessType>(x.BusinessType, ignoreCase: true, out var t)
                       && t == PayeeBusinessType.Enterprise)
            .WithMessage("Doanh nghiệp phải khai tên doanh nghiệp đúng như trên giấy chứng nhận đăng ký kinh doanh.");

        RuleFor(x => x.LegalName).MaximumLength(255);
    }
}
