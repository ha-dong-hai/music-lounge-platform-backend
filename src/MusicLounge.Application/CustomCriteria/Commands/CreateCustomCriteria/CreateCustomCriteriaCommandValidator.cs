using System.Text.Json;
using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.CustomCriteria.Commands.CreateCustomCriteria;

internal sealed class CreateCustomCriteriaCommandValidator : AbstractValidator<CreateCustomCriteriaCommand>
{
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
            .When(x => x.DataType.Equals("Select", StringComparison.OrdinalIgnoreCase)
                || x.DataType.Equals("Range", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.Options)
            .Must(BeValidJson)
            .WithMessage("Options phải là JSON hợp lệ.")
            .When(x => !string.IsNullOrWhiteSpace(x.Options));
    }

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
