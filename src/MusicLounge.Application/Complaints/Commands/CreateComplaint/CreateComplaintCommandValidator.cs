using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Complaints.Commands.CreateComplaint;

internal sealed class CreateComplaintCommandValidator : AbstractValidator<CreateComplaintCommand>
{
    private static readonly string[] ValidTargetTypes = ["show", "venue", "donation", "ticket", "penalty"];

    public CreateComplaintCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.TargetType)
            .Must(t => ValidTargetTypes.Contains(t))
            .WithMessage($"TargetType phải là một trong: {string.Join(", ", ValidTargetTypes)}.");

        RuleFor(x => x.TargetId).GreaterThan(0).WithMessage("TargetId không hợp lệ.");

        // Endpoint nay AllowAnonymous — truoc day TargetId chi check > 0, khong xac minh doi tuong
        // that su ton tai, nen bat ky ai (khong can dang nhap) cung tao duoc complaint tro toi 1
        // show/venue/donation/ticket/penalty khong co that, de lai rac Admin khong the xu ly.
        RuleFor(x => x.TargetId)
            .MustAsync((command, targetId, ct) => TargetExistsAsync(uow, command.TargetType, targetId, ct))
            .When(x => ValidTargetTypes.Contains(x.TargetType) && x.TargetId > 0)
            .WithMessage("Đối tượng bị khiếu nại (TargetType/TargetId) không tồn tại.");

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

    private static Task<bool> TargetExistsAsync(
        IUnitOfWork uow, string targetType, int targetId, CancellationToken ct) => targetType switch
    {
        "show" => uow.Repository<LoungeShow, int>().AnyAsync(s => s.Id == targetId, ct),
        "venue" => uow.Repository<MusicLounge.Domain.Entities.MusicLounge, int>().AnyAsync(l => l.Id == targetId, ct),
        "donation" => uow.Repository<Donation, int>().AnyAsync(d => d.Id == targetId, ct),
        "penalty" => uow.Repository<VenuePenalty, int>().AnyAsync(p => p.Id == targetId, ct),
        // Ticket.Id is a Guid (BaseEntity<Guid>), so this command's `int TargetId` can never hold a
        // real ticket's primary key at all — a pre-existing schema mismatch, not something this
        // rule can validate without changing TargetId's type (a breaking API/FE contract change out
        // of scope here). Left unchecked rather than validated against the wrong column.
        "ticket" => Task.FromResult(true),
        _ => Task.FromResult(false)
    };
}
