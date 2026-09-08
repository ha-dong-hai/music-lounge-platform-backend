using FluentValidation;

namespace MusicLounge.Application.Admin.Commands.ReviewKycDocument;

public sealed class ReviewKycDocumentCommandValidator : AbstractValidator<ReviewKycDocumentCommand>
{
    public ReviewKycDocumentCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);

        RuleFor(x => x.Note)
            .NotEmpty()
            .When(x => !x.Approve)
            .WithMessage("Từ chối hồ sơ phải nêu lý do — người nộp cần biết phải sửa gì để nộp lại.");

        RuleFor(x => x.Note).MaximumLength(1000);
    }
}
