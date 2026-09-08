using FluentValidation;

namespace MusicLounge.Application.Admin.Commands.UpdateSystemConfig;

public sealed class UpdateSystemConfigCommandValidator : AbstractValidator<UpdateSystemConfigCommand>
{
    public UpdateSystemConfigCommandValidator()
    {
        RuleFor(x => x.ConfigKey)
            .NotEmpty().WithMessage("Thiếu khoá cấu hình.")
            .MaximumLength(100);

        RuleFor(x => x.ConfigValue)
            .NotEmpty().WithMessage("Giá trị không được để trống.")
            .MaximumLength(500);

        // Bắt buộc và phải có nội dung thật. SystemConfigHistory.Note là not-null ngay từ thiết kế
        // ban đầu vì một dòng lịch sử không nói vì sao đổi thì gần như vô dụng khi đối chiếu về sau
        // — nhất là với các tham số tiền.
        RuleFor(x => x.Note)
            .NotEmpty().WithMessage("Phải ghi lý do thay đổi — đây là dấu vết kiểm toán, không phải trường tuỳ chọn.")
            .MinimumLength(10).WithMessage("Lý do thay đổi quá ngắn, hãy mô tả rõ vì sao cần đổi giá trị này.")
            .MaximumLength(1000);
    }
}
