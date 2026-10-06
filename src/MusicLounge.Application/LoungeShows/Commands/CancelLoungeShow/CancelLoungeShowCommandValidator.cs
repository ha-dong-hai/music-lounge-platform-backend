using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Commands.CancelLoungeShow;

public sealed class CancelLoungeShowCommandValidator : AbstractValidator<CancelLoungeShowCommand>
{
    public CancelLoungeShowCommandValidator()
    {
        RuleFor(x => x.ShowId).NotEmpty();

        // MLACP-676: khi có gửi lý do thì phải hợp lệ. Bắt buộc hay không tuỳ người huỷ và trạng thái buổi diễn — kiểm ở
        // handler, nơi biết hai điều đó.
        RuleFor(x => x.Reason)
            .Must(r => Enum.TryParse<ShowCancellationReason>(r, ignoreCase: true, out _))
            .WithMessage("Loại lý do huỷ không hợp lệ.")
            .When(x => !string.IsNullOrWhiteSpace(x.Reason));
        RuleFor(x => x.Detail)
            .MaximumLength(1000).WithMessage("Mô tả lý do huỷ tối đa 1000 ký tự.");
        RuleFor(x => x.EvidenceUrl)
            .MaximumLength(500).WithMessage("Đường dẫn bằng chứng tối đa 500 ký tự.");
    }
}
