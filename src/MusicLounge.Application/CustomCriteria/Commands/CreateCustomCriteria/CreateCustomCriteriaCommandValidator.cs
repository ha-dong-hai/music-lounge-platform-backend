using System.Text.Json;
using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.CustomCriteria.Commands.CreateCustomCriteria;

internal sealed class CreateCustomCriteriaCommandValidator : AbstractValidator<CreateCustomCriteriaCommand>
{
    private static bool LaSelect(string dataType)
        => dataType.Equals("Select", StringComparison.OrdinalIgnoreCase);

    private static bool LaRange(string dataType)
        => dataType.Equals("Range", StringComparison.OrdinalIgnoreCase);

    public CreateCustomCriteriaCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);

        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[a-z][a-z0-9_]*$")
            .WithMessage("Key phải là chữ thường, số, gạch dưới, bắt đầu bằng chữ (vd: performance_language).");

        RuleFor(x => x.DataType)
            .Must(d => Enum.TryParse<CustomCriteriaDataType>(d, true, out _))
            .WithMessage($"DataType phải là một trong: {string.Join(", ", Enum.GetNames<CustomCriteriaDataType>())}.");

        // Select/Range can lay du lieu tu Options (JSON) de hien thi form dung — bat buoc phai co
        // va phai la JSON hop le, khac voi Boolean/Text khong can Options.
        RuleFor(x => x.Options)
            .NotEmpty()
            .WithMessage("Options bắt buộc với DataType Select/Range.")
            .When(x => LaSelect(x.DataType) || LaRange(x.DataType));

        RuleFor(x => x.Options)
            .Must(BeValidJson)
            .WithMessage("Options phải là JSON hợp lệ.")
            .When(x => !string.IsNullOrWhiteSpace(x.Options));

        // "JSON hợp lệ" là chưa đủ, và đây từng là một lỗ thật: tiêu chí Select tạo với Options là
        // [1,2,3] vẫn được nhận, rồi khi gắn giá trị cho buổi diễn thì CustomCriteriaValue đọc không ra
        // danh sách nên BỎ QUA việc đối chiếu — cố ý không bịa luật từ một quy tắc không đọc được. Kết
        // quả là một tiêu chí ô chọn mà mọi chuỗi đều lọt, qua cửa API công khai chứ không phải chỉ dòng
        // dữ liệu cũ. Chặn ngay lúc tạo mới là chặn tại gốc: dùng ĐÚNG hàm đọc mà lúc đối chiếu sẽ dùng,
        // nên "tạo được" và "đối chiếu được" không thể lệch nhau. (MLACP-472)
        RuleFor(x => x.Options)
            .Must(o => CustomCriteriaValue.DanhSachLuaChon(o) is { Count: > 0 })
            .WithMessage("Options của DataType Select phải là mảng JSON các chuỗi, vd: [\"Bolero\",\"Acoustic\"].")
            .When(x => LaSelect(x.DataType) && !string.IsNullOrWhiteSpace(x.Options));

        RuleFor(x => x.Options)
            .Must(CustomCriteriaValue.LaDoiTuongJson)
            .WithMessage("Options của DataType Range phải là đối tượng JSON, vd: {\"min\":1,\"max\":5}.")
            .When(x => LaRange(x.DataType) && !string.IsNullOrWhiteSpace(x.Options));

        // min > max tạo ra một tiêu chí không giá trị nào thoả: người dùng gõ gì cũng bị từ chối, và lỗi
        // hiện ra ở màn hình sửa buổi diễn chứ không ở chỗ gõ sai. Bắt ngay tại chỗ gõ.
        RuleFor(x => x.Options)
            .Must(BienKhongNguoc)
            .WithMessage("Options của DataType Range có min lớn hơn max.")
            .When(x => LaRange(x.DataType) && CustomCriteriaValue.LaDoiTuongJson(x.Options));
    }

    private static bool BienKhongNguoc(string? options)
        => CustomCriteriaValue.Khoang(options) is not { Min: { } min, Max: { } max } || min <= max;

    private static bool BeValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
