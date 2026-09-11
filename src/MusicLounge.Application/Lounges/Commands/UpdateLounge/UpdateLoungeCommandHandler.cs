using System.Globalization;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Tickets;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Domain.ValueObjects;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.UpdateLounge;

internal sealed class UpdateLoungeCommandHandler : IRequestHandler<UpdateLoungeCommand, Unit>
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ILogger<UpdateLoungeCommandHandler> _logger;

    public UpdateLoungeCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        ILogger<UpdateLoungeCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateLoungeCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await repo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        var oldName = lounge.Name;
        var oldAddress = lounge.Address;

        var newAddress = new VenueAddress
        {
            Street = request.Street,
            Ward = request.Ward,
            District = request.District,
            City = request.City,
            Latitude = request.Latitude,
            Longitude = request.Longitude
        };

        // MLACP-352. Truoc day ten va dia chi bi ghi de lang le: khong bao ai, khong luu vet. So sanh
        // tung truong, bo qua hoa/thuong va khoang trang thua — luu lai y nguyen khong phai la "doi".
        var nameChanged = !SameText(oldName, request.Name);
        var addressChanged = !SameText(oldAddress.Street, newAddress.Street)
                             || !SameText(oldAddress.Ward, newAddress.Ward)
                             || !SameText(oldAddress.District, newAddress.District)
                             || !SameText(oldAddress.City, newAddress.City);
        var pinMoved = oldAddress.Latitude != newAddress.Latitude
                       || oldAddress.Longitude != newAddress.Longitude;

        var oldFullAddress = oldAddress.FullAddress;

        lounge.Name = request.Name;
        lounge.Description = request.Description;
        lounge.AtmosphereId = request.AtmosphereId;
        lounge.Address = newAddress;
        repo.Update(lounge);

        var ticketHoldersTold = addressChanged
            ? await TellTicketHoldersAsync(lounge, oldFullAddress, ct)
            : 0;

        if (nameChanged || addressChanged || pinMoved)
        {
            _logger.LogInformation(
                "Phong tra doi thong tin nhan dien — LoungeId={LoungeId} DoiTen={NameChanged} " +
                "DoiDiaChi={AddressChanged} DoiToaDo={PinMoved} SoLuotBaoNguoiGiuVe={Told} BoiUserId={UserId}",
                lounge.Id, nameChanged, addressChanged, pinMoved, ticketHoldersTold, _currentUser.UserId);

            await TellAdminsAsync(
                lounge, oldName, oldFullAddress, oldAddress.Latitude, oldAddress.Longitude,
                nameChanged, addressChanged, pinMoved, ticketHoldersTold, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }

    /// <summary>
    /// Người giữ vé vào cửa của buổi diễn chưa diễn phải biết địa chỉ mới — không thì họ tới sai chỗ.
    ///
    /// <para>Đi đúng khuôn <c>RescheduleLoungeShow</c> đã dùng cho đổi lịch: vé vẫn có hiệu lực; người mua trước
    /// thay đổi được huỷ và hoàn 100% tới hạn huỷ theo lịch hiện tại, hoặc tới giờ diễn (MLACP-372). Chính sách của
    /// buổi diễn cho người mua sau không bị sửa.
    /// Ticketmaster cũng vậy: <i>"If an event is rescheduled or moved, your tickets ... are still
    /// valid"</i>, và ban tổ chức quyết có cho hoàn hay không.</para>
    ///
    /// <para>Chỉ vé <b>vào cửa</b>: người xem livestream không phải tới phòng trà. Chỉ buổi diễn
    /// <c>Published</c> chưa qua giờ kết thúc — cùng phạm vi với đổi lịch; buổi đang diễn thì khán giả
    /// đã ở đó. Buổi không có ai giữ vé vào cửa thì không đụng tới chính sách của nó.</para>
    /// </summary>
    private async Task<int> TellTicketHoldersAsync(
        MusicLoungeEntity lounge, string oldFullAddress, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var showRepo = _uow.Repository<LoungeShow, int>();

        // Lọc trạng thái phía server, so thời gian phía client — provider SQLite trong test không dịch
        // được phép so enum kèm DateTimeOffset trong cùng một truy vấn.
        var upcoming = (await showRepo.FindAsync(
                s => s.LoungeId == lounge.Id
                     && s.Status == LoungeShowStatus.Published
                     && s.Format != LoungeShowFormat.Online, ct))
            .Where(s => ShowSchedule.EffectiveEnd(s) > now)
            .ToList();

        var told = 0;

        foreach (var show in upcoming)
        {
            var tickets = await _uow.Repository<Ticket, Guid>().FindAsync(
                t => t.ShowId == show.Id
                     && t.Status == TicketStatus.Confirmed
                     && t.BuyerId != null
                     && t.Tier.AccessType == AccessType.Physical, ct);
            if (tickets.Count == 0) continue;

            // MLACP-372: trước đây bật CancellationAllowed vĩnh viễn và hoàn theo tỉ lệ của phòng trà — như đổi lịch.
            // Nay chỉ ghi thời điểm đổi; quyền hoàn 100% của người mua trước nằm ở TicketRefundPolicy.FullRefundUntil.
            show.VenueMovedAt = now;
            showRepo.Update(show);

            // Đúng mốc mà CancelTicket áp cho người mua trước thay đổi.
            var terms = TicketRefundPolicy.DescribeFullRefundWindow(TicketRefundPolicy.FullRefundWindowEnd(show, now));
            var payers = await TicketRefundRecipients.PayersAsync(_uow, tickets, ct);

            foreach (var holding in tickets.GroupBy(t => t.BuyerId!.Value))
            {
                var transferredNote = holding.Any(t => TicketRefundRecipients.WasTransferred(t, payers))
                    ? TicketRefundRecipients.TransferredHolderCancelNote
                    : "";
                await _notifications.NotifyAsync(
                    holding.Key,
                    NotificationType.EventVenueChanged,
                    "Phòng trà đã đổi địa chỉ",
                    $"\"{show.Name}\" sẽ diễn ở địa chỉ mới: {lounge.Address.FullAddress}. " +
                    $"Địa chỉ cũ: {oldFullAddress}. Vé của bạn vẫn có hiệu lực. {terms}{transferredNote}",
                    referenceType: "show",
                    referenceId: show.Id.ToString(),
                    ct: ct);
                told++;
            }
        }

        return told;
    }

    /// <summary>
    /// Hồ sơ phòng trà được Admin duyệt (MLACP-307) dựa trên chính tên và địa chỉ này — đổi sau khi
    /// duyệt thì Admin phải biết. Hệ thống không có bảng nhật ký thay đổi nào, nên thông báo này (kèm
    /// giá trị trước/sau) là vết duy nhất còn lại của thay đổi.
    /// </summary>
    private async Task TellAdminsAsync(
        MusicLoungeEntity lounge, string oldName, string oldFullAddress, double? oldLatitude,
        double? oldLongitude, bool nameChanged, bool addressChanged, bool pinMoved, int ticketHoldersTold,
        CancellationToken ct)
    {
        var admins = await _uow.Repository<User, int>().FindAsync(
            u => u.Role == UserRole.Admin && u.IsActive && u.Id != _currentUser.UserId, ct);
        if (admins.Count == 0) return;

        var changes = new List<string>();
        if (nameChanged)
            changes.Add($"tên \"{oldName}\" → \"{lounge.Name}\"");
        if (addressChanged)
            changes.Add($"địa chỉ \"{oldFullAddress}\" → \"{lounge.Address.FullAddress}\"");
        if (pinMoved)
            changes.Add(string.Create(CultureInfo.InvariantCulture,
                $"toạ độ ({oldLatitude}, {oldLongitude}) → ({lounge.Address.Latitude}, {lounge.Address.Longitude})"));

        var body = $"Phòng trà #{lounge.Id} vừa đổi {string.Join("; ", changes)}. " +
                   (ticketHoldersTold > 0
                       ? $"Đã gửi {ticketHoldersTold} lượt báo tới người giữ vé vào cửa của các buổi diễn sắp tới. "
                       : "") +
                   "Hồ sơ phòng trà đã được duyệt dựa trên thông tin cũ — cần xem lại nếu thay đổi không " +
                   "khớp giấy phép kinh doanh.";

        foreach (var admin in admins)
        {
            await _notifications.NotifyAsync(
                admin.Id,
                NotificationType.VenueIdentityChanged,
                "Phòng trà đổi thông tin nhận diện",
                body,
                referenceType: "lounge",
                referenceId: lounge.Id.ToString(),
                ct: ct);
        }
    }

    private static bool SameText(string? a, string? b)
        => string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value) => Whitespace.Replace((value ?? string.Empty).Trim(), " ");
}
