using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Tickets.Commands.HoldTicket;

internal sealed class HoldTicketCommandValidator : AbstractValidator<HoldTicketCommand>
{
    public HoldTicketCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.PriceId)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("PriceId không hợp lệ.")
            .MustAsync(async (priceId, ct) => await uow.Repository<TicketPrice, int>().AnyAsync(p => p.Id == priceId, ct))
            .WithMessage("PriceId không tồn tại.");
        RuleFor(x => x.Quantity).InclusiveBetween(1, 10).WithMessage("Số lượng vé phải từ 1 đến 10.");
    }
}
