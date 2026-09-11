using FluentValidation;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Performers.Commands.RespondToPerformerConfirmation;

/// <param name="Decision">Confirm (đúng / đã nhận) hoặc Dispute (không phải của tôi / chưa nhận).</param>
/// <param name="ConsentToDataProcessing">Đồng ý cho MusicLounge xử lý email và thông tin tài khoản nhận
/// tiền của nghệ sĩ — bắt buộc (Luật Bảo vệ dữ liệu cá nhân 2025).</param>
public sealed record RespondToPerformerConfirmationCommand(
    string Token, string Decision, bool ConsentToDataProcessing, string? Note) : ICommand;

internal sealed class RespondToPerformerConfirmationCommandValidator
    : AbstractValidator<RespondToPerformerConfirmationCommand>
{
    public RespondToPerformerConfirmationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Decision)
            .Must(d => string.Equals(d, "Confirm", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(d, "Dispute", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Decision phải là Confirm hoặc Dispute.");
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
    }
}
