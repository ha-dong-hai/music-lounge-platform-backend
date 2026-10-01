using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Application.Lounges;

/// <summary>
/// MLACP-521. Phần địa chỉ chung của lệnh tạo và lệnh sửa phòng trà — MỘT bộ luật kiểm và MỘT cách dựng địa chỉ cho cả
/// hai, để tạo và sửa không thể lệch nhau (trước đây mỗi lệnh tự chép luật của mình).
///
/// <para>Hai cách gửi địa chỉ, cùng được nhận:</para>
/// <list type="bullet">
/// <item><b>Theo danh mục</b> (cách mới): <c>ProvinceCode</c> (+ <c>WardCode</c>) theo QĐ 19/2025/QĐ-TTg. Tên tỉnh/xã lấy từ
/// danh mục, <c>District</c> bị bỏ qua vì từ 01/7/2025 không còn cấp huyện.</item>
/// <item><b>Gõ tay</b> (cách cũ, giữ để giao diện chưa cập nhật không hỏng): <c>City</c>/<c>Ward</c>/<c>District</c> dạng chữ.</item>
/// </list>
/// </summary>
public interface ILoungeAddressInput
{
    string Street { get; }
    string? Ward { get; }
    string? District { get; }
    string? City { get; }
    double? Latitude { get; }
    double? Longitude { get; }
    string? ProvinceCode { get; }
    string? WardCode { get; }
}

internal static class LoungeAddressRules
{
    internal static void AddLoungeAddressRules<T>(this AbstractValidator<T> v, IAdministrativeUnitCatalog catalog)
        where T : ILoungeAddressInput
    {
        v.RuleFor(x => x.Street).NotEmpty().MaximumLength(255);
        // Cai cach hanh chinh 2025 bo cap Quan/Huyen (NQ 202/2025/QH15) — khong bat buoc, van gioi han do dai neu co.
        v.RuleFor(x => x.District).MaximumLength(100);
        // Ward chua tung bat buoc (giu nguyen de giao dien cu khong hong); City bat buoc TRU KHI da chon tinh theo ma.
        v.RuleFor(x => x.Ward).MaximumLength(100);
        v.RuleFor(x => x.City).NotEmpty().MaximumLength(100).When(x => string.IsNullOrWhiteSpace(x.ProvinceCode));
        v.RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        v.RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);

        v.RuleFor(x => x.ProvinceCode)
            .Must(code => catalog.FindProvince(code) is not null)
            .When(x => !string.IsNullOrWhiteSpace(x.ProvinceCode))
            .WithMessage("Mã tỉnh/thành phố không có trong danh mục hành chính hiện hành (QĐ 19/2025/QĐ-TTg).");

        // Ba RuleFor RIÊNG, không nối chuỗi: .When() của FluentValidation mặc định áp lên MỌI luật đứng trước nó trong
        // cùng chuỗi — nối lại thì điều kiện của luật cuối khoá luôn hai luật đầu (test MaSai_Tra400_CoLyDo bắt được).
        v.RuleFor(x => x.WardCode)
            .Must((x, _) => !string.IsNullOrWhiteSpace(x.ProvinceCode))
            .When(x => !string.IsNullOrWhiteSpace(x.WardCode))
            .WithMessage("Cần chọn tỉnh/thành phố trước khi chọn phường/xã.");
        v.RuleFor(x => x.WardCode)
            .Must(code => catalog.FindWard(code) is not null)
            .When(x => !string.IsNullOrWhiteSpace(x.WardCode))
            .WithMessage("Mã phường/xã không có trong danh mục hành chính hiện hành (QĐ 19/2025/QĐ-TTg).");
        v.RuleFor(x => x.WardCode)
            .Must((x, code) => catalog.FindWard(code)?.ProvinceCode == catalog.FindProvince(x.ProvinceCode)?.Code)
            .When(x => catalog.FindWard(x.WardCode) is not null && catalog.FindProvince(x.ProvinceCode) is not null)
            .WithMessage("Phường/xã đã chọn không thuộc tỉnh/thành phố đã chọn.");
    }

    /// <summary>Có mã thì tên lấy từ danh mục (không tin chữ client gửi kèm); không có mã thì giữ nguyên chữ như trước.</summary>
    internal static VenueAddress BuildLoungeAddress(this IAdministrativeUnitCatalog catalog, ILoungeAddressInput input)
    {
        var province = catalog.FindProvince(input.ProvinceCode);
        var ward = province is null ? null : catalog.FindWard(input.WardCode);
        return new VenueAddress
        {
            Street = input.Street,
            Ward = ward?.Name ?? input.Ward ?? string.Empty,
            District = province is null ? input.District ?? string.Empty : string.Empty,
            City = province?.Name ?? input.City ?? string.Empty,
            ProvinceCode = province?.Code,
            WardCode = ward?.Code,
            Latitude = input.Latitude,
            Longitude = input.Longitude
        };
    }
}
