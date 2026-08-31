using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Complaints.Commands.ResolveComplaint;

internal sealed class ResolveComplaintCommandValidator : AbstractValidator<ResolveComplaintCommand>
{
    private static readonly string[] AllowedStatuses = ["Investigating", "Resolved", "Rejected"];

    public ResolveComplaintCommandValidator()
    {
        RuleFor(x => x.ComplaintId).GreaterThan(0);

        RuleFor(x => x.Status)
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage($"Status phải là một trong: {string.Join(", ", AllowedStatuses)}.");

        RuleFor(x => x.ResolvedAction)
            .Must(a => a is null || Enum.TryParse<ComplaintResolvedAction>(a, true, out _))
            .WithMessage("ResolvedAction không hợp lệ.");

        RuleFor(x => x.ResolvedAction)
            .NotNull().WithMessage("Cần chọn ResolvedAction khi đánh dấu Resolved.")
            .When(x => x.Status == "Resolved");

        RuleFor(x => x.Resolution)
            .MaximumLength(2000);
    }
}
