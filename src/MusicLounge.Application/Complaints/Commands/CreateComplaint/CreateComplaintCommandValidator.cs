using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Complaints.Commands.CreateComplaint;

internal sealed class CreateComplaintCommandValidator : AbstractValidator<CreateComplaintCommand>
{
    // MLACP-192: "livestream" them cho phep khan gia bao cao noi dung vi pham dang phat truc tiep,
    // xac minh ton tai qua Livestream.Id (khac voi "show", tro toi LoungeShow.Id).
    private static readonly string[] ValidTargetTypes = ["show", "venue", "donation", "ticket", "penalty", "livestream"];

    public CreateComplaintCommandValidator(IUnitOfWork uow, ISystemConfigService config)
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

        // MLACP-197: chi cho tao khieu nai "donate chua duoc tra" khi donate that su chua tra
        // (Status != PerformerPaid) VA da qua so ngay cho phep (system_config donation_hold_days,
        // dung chung 1 nguon voi phan loai Overdue cua GetOwnerDonationHistoryQueryHandler —
        // MLACP-200 — de tranh 2 noi tinh "qua han" ra 2 moc thoi gian khac nhau cho cung 1 donate).
        RuleFor(x => x.TargetId)
            .MustAsync((command, targetId, ct) => DonationEligibleForNotPaidComplaintAsync(uow, config, targetId, ct))
            .When(x => x.TargetType == "donation"
                && x.TargetId > 0
                && Enum.TryParse<ComplaintCategory>(x.Category, true, out var cat)
                && cat == ComplaintCategory.DonationNotPaid)
            .WithMessage("Chỉ có thể khiếu nại donate chưa được trả sau khi đã quá hạn giữ tiền quy định.");

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
        "livestream" => uow.Repository<Livestream, int>().AnyAsync(l => l.Id == targetId, ct),
        _ => Task.FromResult(false)
    };

    private static async Task<bool> DonationEligibleForNotPaidComplaintAsync(
        IUnitOfWork uow, ISystemConfigService config, int donationId, CancellationToken ct)
    {
        var donation = await uow.Repository<Donation, int>().GetByIdAsync(donationId, ct);
        if (donation is null || donation.Status == DonationStatus.PerformerPaid) return false;
        if (donation.PaymentConfirmedAt is null) return false;

        var holdDays = await config.GetIntAsync(ConfigKeys.DonationHoldDays, 14, ct);
        return DateTimeOffset.UtcNow > donation.PaymentConfirmedAt.Value.AddDays(holdDays);
    }
}
