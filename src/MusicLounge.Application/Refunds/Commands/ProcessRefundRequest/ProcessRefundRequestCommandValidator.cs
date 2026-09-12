using FluentValidation;
using MusicLounge.Application.Common;

namespace MusicLounge.Application.Refunds.Commands.ProcessRefundRequest;

internal sealed class ProcessRefundRequestCommandValidator : AbstractValidator<ProcessRefundRequestCommand>
{
    public ProcessRefundRequestCommandValidator()
    {
        RuleFor(x => x.RefundRequestId).GreaterThan(0);

        RuleFor(x => x.Decision)
            .Must(d => d is "Approved" or "Rejected")
            .WithMessage("Decision phải là 'Approved' hoặc 'Rejected'.");

        RuleFor(x => x.ApprovedAmount)
            .GreaterThan(0)
            .When(x => x.ApprovedAmount is not null)
            .WithMessage("ApprovedAmount phải lớn hơn 0.");

        // MLACP-332. Số tiền này đi thẳng vào VnPayService.RefundAsync. Khác đường donate, ở đây
        // KHÔNG có chốt đối chiếu số tiền nào cả — nên số lẻ không bị chặn lại mà ghi vào sổ cái
        // một con số khác với số VNPay thật sự hoàn. Rule riêng để không dính vào .When(...) ở
        // trên, vì .When mặc định áp cho toàn bộ các validator đứng trước nó trong cùng RuleFor.
        RuleFor(x => x.ApprovedAmount).MustBeWholeDong();

        RuleFor(x => x.ResolutionNote)
            .MaximumLength(500)
            .When(x => x.ResolutionNote is not null);

        RuleFor(x => x.ClientIpAddress).NotEmpty().WithMessage("Địa chỉ IP không được rỗng.");

        // MLACP-384: ghi nhan chuyen khoan thu cong chi co nghia khi duyet — tu choi thi khong co khoan tien nao.
        RuleFor(x => x.ManualTransferReference)
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithMessage("ManualTransferReference không được rỗng khi được gửi.")
            .MaximumLength(100)
            .When(x => x.ManualTransferReference is not null);

        RuleFor(x => x.Decision)
            .Equal("Approved")
            .When(x => x.ManualTransferReference is not null)
            .WithMessage("Chỉ ghi nhận chuyển khoản thủ công khi duyệt (Approved) yêu cầu hoàn tiền.");
    }
}
