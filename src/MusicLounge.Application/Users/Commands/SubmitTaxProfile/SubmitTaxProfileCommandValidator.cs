using System.Text.RegularExpressions;
using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Users.Commands.SubmitTaxProfile;

/// <summary>
/// MLACP-660. Mã định danh thuế theo loại người nộp thuế, đúng quy định từ 01/7/2025 (Thông tư 86/2024/TT-BTC): số định
/// danh cá nhân 12 chữ số (số CCCD) THAY mã số thuế của cá nhân, hộ gia đình và hộ kinh doanh; doanh nghiệp vẫn dùng mã số
/// thuế 10 chữ số (thêm "-" và 3 chữ số cho đơn vị trực thuộc).
///
/// <para>Trước đây mọi loại hình chỉ nhận mã 10 chữ số — hộ kinh doanh đăng ký sau 01/7/2025 (chỉ có số định danh) không
/// khai được hồ sơ thuế (rà soát 05/10/2026).</para>
/// </summary>
public sealed partial class SubmitTaxProfileCommandValidator : AbstractValidator<SubmitTaxProfileCommand>
{
    // Doanh nghiệp: 10 chữ số, hoặc 10 + "-" + 3 chữ số cho đơn vị trực thuộc.
    [GeneratedRegex(@"^\d{10}(-\d{3})?$")]
    private static partial Regex EnterpriseTaxCodePattern();

    // Hộ/cá nhân: số định danh cá nhân 12 chữ số.
    [GeneratedRegex(@"^\d{12}$")]
    private static partial Regex PersonalIdPattern();

    private static PayeeBusinessType? Declared(SubmitTaxProfileCommand x)
        => Enum.TryParse<PayeeBusinessType>(x.BusinessType, ignoreCase: true, out var t) ? t : null;

    public SubmitTaxProfileCommandValidator()
    {
        RuleFor(x => x.BusinessType)
            .Must(t => Enum.TryParse<PayeeBusinessType>(t, ignoreCase: true, out _))
            .WithMessage($"Loại hình kinh doanh phải là một trong: {string.Join(", ", Enum.GetNames<PayeeBusinessType>())}.");

        RuleFor(x => x.TaxCode)
            .NotEmpty().WithMessage("Mã số thuế không được để trống.");

        RuleFor(x => x.TaxCode)
            .Must(c => PersonalIdPattern().IsMatch(c.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.TaxCode) && Declared(x) == PayeeBusinessType.HouseholdOrIndividual)
            .WithMessage("Hộ kinh doanh và cá nhân dùng số định danh cá nhân (số CCCD 12 chữ số) làm mã số thuế, theo quy định từ 01/7/2025.");

        RuleFor(x => x.TaxCode)
            .Must(c => EnterpriseTaxCodePattern().IsMatch(c.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.TaxCode) && Declared(x) == PayeeBusinessType.Enterprise)
            .WithMessage("Mã số thuế doanh nghiệp gồm 10 chữ số, hoặc 10 chữ số kèm 3 chữ số đơn vị trực thuộc (ví dụ 0123456789-001).");

        RuleFor(x => x.LegalName)
            .NotEmpty()
            .When(x => Declared(x) == PayeeBusinessType.Enterprise)
            .WithMessage("Doanh nghiệp phải khai tên doanh nghiệp đúng như trên giấy chứng nhận đăng ký kinh doanh.");

        RuleFor(x => x.LegalName).MaximumLength(255);
    }
}
