using FluentValidation;

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

        RuleFor(x => x.ClientIpAddress).NotEmpty().WithMessage("Địa chỉ IP không được rỗng.");
    }
}
