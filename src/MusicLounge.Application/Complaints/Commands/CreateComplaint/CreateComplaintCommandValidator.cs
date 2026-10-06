using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Donations;
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

        RuleFor(x => x.TargetId).NotEmpty().WithMessage("TargetId không hợp lệ.");

        // Endpoint nay AllowAnonymous — truoc day TargetId chi check > 0, khong xac minh doi tuong
        // that su ton tai, nen bat ky ai (khong can dang nhap) cung tao duoc complaint tro toi 1
        // show/venue/donation/ticket/penalty khong co that, de lai rac Admin khong the xu ly.
        RuleFor(x => x.TargetId)
            .MustAsync((command, targetId, ct) => TargetExistsAsync(uow, command.TargetType, targetId, ct))
            .When(x => ValidTargetTypes.Contains(x.TargetType) && x.TargetId != Guid.Empty)
            .WithMessage("Đối tượng bị khiếu nại (TargetType/TargetId) không tồn tại.");

        // MLACP-197: chi cho tao khieu nai "donate chua duoc tra" khi donate that su chua tra
        // (Status != PerformerPaid) VA da qua so ngay cho phep (system_config donation_hold_days,
        // dung chung 1 nguon voi phan loai Overdue cua GetOwnerDonationHistoryQueryHandler —
        // MLACP-200 — de tranh 2 noi tinh "qua han" ra 2 moc thoi gian khac nhau cho cung 1 donate).
        RuleFor(x => x.TargetId)
            .MustAsync((command, targetId, ct) => DonationEligibleForNotPaidComplaintAsync(uow, config, targetId, ct))
            .When(x => x.TargetType == "donation"
                && x.TargetId != Guid.Empty
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
        IUnitOfWork uow, string targetType, Guid targetId, CancellationToken ct) => targetType switch
    {
        "show" => uow.Repository<LoungeShow, Guid>().AnyAsync(s => s.Id == targetId, ct),
        "venue" => uow.Repository<MusicLounge.Domain.Entities.MusicLounge, Guid>().AnyAsync(l => l.Id == targetId, ct),
        "donation" => uow.Repository<Donation, Guid>().AnyAsync(d => d.Id == targetId, ct),
        "penalty" => uow.Repository<VenuePenalty, Guid>().AnyAsync(p => p.Id == targetId, ct),
        // MLACP-515: trước đây TargetId là int còn Ticket.Id vốn đã là Guid nên không kiểm được, phải để lọt. Nay mọi khoá
        // là GUID — kiểm như các loại khác. (Khiếu nại "ticket" cũ mang số int không trỏ được vé nào; migration giữ
        // nguyên-giá-trị-đã-đổi-dạng cho chúng, xem Mlacp515GuidKeys.)
        "ticket" => uow.Repository<Ticket, Guid>().AnyAsync(t => t.Id == targetId, ct),
        // MLACP-680: trang xem là /livestream/<mã BUỔI DIỄN>, nên người dán đường dẫn trang xem mang mã buổi diễn chứ
        // không phải Livestream.Id — trước đây bị báo "không tồn tại". Nhận cả hai; handler đổi về Livestream.Id
        // (một buổi diễn có tối đa một livestream — chỉ mục duy nhất LoungeShowId, nên không mơ hồ).
        "livestream" => uow.Repository<Livestream, Guid>().AnyAsync(l => l.Id == targetId || l.LoungeShowId == targetId, ct),
        _ => Task.FromResult(false)
    };

    private static async Task<bool> DonationEligibleForNotPaidComplaintAsync(
        IUnitOfWork uow, ISystemConfigService config, Guid donationId, CancellationToken ct)
    {
        var donation = await uow.Repository<Donation, Guid>().GetByIdAsync(donationId, ct);
        if (donation is null || donation.Status == DonationStatus.PerformerPaid) return false;

        // MLACP-362: cung mot hạn voi nhac nho/canh cao/lich su cua chu, tinh tu luc phong tra that su
        // nhan tien. Truoc day tinh tu luc VNPay xac nhan voi mac dinh 14 ngay (noi khac 7). Nen tang
        // chua chuyen tien cho phong tra thi chua co hạn nao de phong tra tre.
        var holdDays = await DonationPayoutDeadline.HoldDaysAsync(config, ct);
        var releaseTimes = await DonationPayoutDeadline.PayoutReleaseTimesAsync(uow, [donationId], ct);
        var dueAt = DonationPayoutDeadline.DueAt(DonationPayoutDeadline.ReceivedAt(donation, releaseTimes), holdDays);
        return dueAt is { } due && DateTimeOffset.UtcNow > due;
    }
}
