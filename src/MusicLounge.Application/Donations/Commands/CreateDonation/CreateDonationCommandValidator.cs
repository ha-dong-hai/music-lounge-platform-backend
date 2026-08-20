using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Donations.Commands.CreateDonation;

public sealed class CreateDonationCommandValidator : AbstractValidator<CreateDonationCommand>
{
    public CreateDonationCommandValidator(IUnitOfWork uow, ISystemConfigService config)
    {
        RuleFor(x => x.PerformanceId)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("PerformanceId không hợp lệ.")
            .MustAsync(async (performanceId, ct) =>
                await uow.Repository<Performance, int>().AnyAsync(p => p.Id == performanceId, ct))
            .WithMessage("PerformanceId không tồn tại.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền tối thiểu là 1đ.")
            .MustAsync(async (_, amount, context, ct) =>
            {
                var max = await config.GetDecimalAsync(ConfigKeys.DonationMaxAmount, 50_000_000m, ct);
                context.MessageFormatter.AppendArgument("MaxAmount", max);
                return amount <= max;
            })
            .WithMessage("Số tiền donate phải từ 1đ đến {MaxAmount}đ.");

        RuleFor(x => x.Message)
            .MaximumLength(500)
            .When(x => x.Message is not null);
    }
}
