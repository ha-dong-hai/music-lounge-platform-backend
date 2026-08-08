using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Complaints.Commands.CreateComplaint;

internal sealed class CreateComplaintCommandValidator : AbstractValidator<CreateComplaintCommand>
{
    private static readonly string[] ValidTargetTypes = ["show", "venue", "donation", "ticket", "penalty"];

    public CreateComplaintCommandValidator()
    {
        RuleFor(x => x.TargetType)
            .Must(t => ValidTargetTypes.Contains(t))
            .WithMessage($"TargetType phải là một trong: {string.Join(", ", ValidTargetTypes)}.");

        RuleFor(x => x.TargetId).GreaterThan(0).WithMessage("TargetId không hợp lệ.");

        RuleFor(x => x.Category)
            .Must(c => Enum.TryParse<ComplaintCategory>(c, true, out _))
            .WithMessage("Category không hợp lệ.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Vui lòng mô tả khiếu nại.")
            .MaximumLength(2000);

        RuleFor(x => x.ContactPhone)
            .MaximumLength(20)
            .When(x => x.ContactPhone is not null);
    }
}
