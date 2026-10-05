using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Livestreams.Queries.GetLivestreamDetail;

internal sealed class GetLivestreamDetailQueryHandler : IRequestHandler<GetLivestreamDetailQuery, LivestreamDetailDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IBackgroundJobService _backgroundJobs;
    private readonly ISystemConfigService _systemConfig;
    private readonly ILivestreamServiceFactory _streams;

    public GetLivestreamDetailQueryHandler(
        IUnitOfWork uow,
        ILivestreamRepository livestreamRepo,
        ICurrentUserService currentUser,
        IBackgroundJobService backgroundJobs,
        ISystemConfigService systemConfig,
        ILivestreamServiceFactory streams)
    {
        _streams = streams;
        _uow = uow;
        _livestreamRepo = livestreamRepo;
        _currentUser = currentUser;
        _backgroundJobs = backgroundJobs;
        _systemConfig = systemConfig;
    }

    public async Task<LivestreamDetailDto> Handle(GetLivestreamDetailQuery request, CancellationToken ct)
    {
        var livestream = await _livestreamRepo.GetByIdWithDetailsAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException("Livestream", request.LivestreamId);

        // HLS URL is only visible to users with a valid livestream ticket, or to Staff/Owner of
        // THIS venue who need to monitor the stream — was previously any Staff/Admin account
        // regardless of venue, letting Staff of venue A watch venue B's paid livestream for free.
        var userHasAccess = _currentUser.Role == Roles.Admin;
        var isGenuineTicketHolder = false;
        if (!userHasAccess)
        {
            var lounge = await _uow.Repository<MusicLoungeEntity, Guid>()
                .GetByIdAsync(livestream.LoungeShow.LoungeId, ct);
            var isVenueOperator = lounge is not null
                && VenueOperatorAccess.CanOperate(_currentUser, livestream.LoungeShow.LoungeId, lounge.OwnerId);

            // MLACP-117: livestream mien phi (IsFree) khong yeu cau ve — bat ky khan gia da dang
            // nhap nao cung xem duoc, khong can qua HasViewerAccessAsync (kiem tra do chi danh cho
            // luong PPV co ban ve). Truoc khi co dieu kien nay, mot livestream mien phi van bi tu
            // choi HlsUrl neu nguoi xem chua tung mua ve Livestream-tier cho show do — mau thuan
            // truc tiep voi DONE WHEN "Stream mien phi: cho xem khong can token".
            if (isVenueOperator || livestream.IsFree)
            {
                userHasAccess = true;
            }
            else
            {
                isGenuineTicketHolder = _currentUser.IsAuthenticated
                    && await _livestreamRepo.HasViewerAccessAsync(request.LivestreamId, _currentUser.UserId, ct);
                userHasAccess = isGenuineTicketHolder;
            }
        }

        var now = DateTimeOffset.UtcNow;
        string? viewingSessionId = null;

        // Only a real ticket-holding viewer actually "watching" counts as a recommendation signal —
        // Admin/Staff/Owner hitting this endpoint to monitor their own stream isn't behavioural
        // interest and would otherwise pollute the collaborative-filtering matrix.
        if (isGenuineTicketHolder)
        {
            _backgroundJobs.EnqueueLogUserBehaviour(
                _currentUser.UserId, livestream.LoungeShowId, BehaviourAction.WatchLivestream);

            // MLACP-140: day la thoi diem duy nhat chung minh chu ve that su nhan duoc HlsUrl phat —
            // tuong duong "check-in" cho ve Livestream (khong co quay quet QR nao khac ton tai cho
            // ho). Chuyen ve sang Used de RateShowCommandHandler cho phep danh gia.
            _backgroundJobs.EnqueueLivestreamCheckIn(_currentUser.UserId, livestream.LoungeShowId);

            // MLACP-513: buổi đã ở trạng thái cuối thì không còn gì để xem — không mở phiên, nên cũng không áp giới hạn
            // thiết bị. Trước đây khán giả có vé tải lại trang sau khi buổi kết thúc bị 422 "đang xem trên 2 thiết bị"
            // thay vì thấy màn "đã kết thúc" (fe đo bằng Mux thật, M-442). Scheduled vẫn mở phiên: khán giả vào trang trước
            // giờ phát cần sẵn phiên khi luồng bắt đầu (chưa có sự kiện hub "bắt đầu phát" để client lấy phiên lúc đó).
            if (livestream.Status is not (LivestreamStatus.Ended or LivestreamStatus.Terminated or LivestreamStatus.Failed))
                viewingSessionId = await OpenViewingSessionAsync(request.LivestreamId, request.ViewingSessionId, now, ct);
        }

        // MLACP-647: người có quyền nhận link có hạn (luồng có chữ ký thì kèm token) — đủ dài để xem trọn buổi kể cả kéo
        // giờ, nhưng không quá một ngày: link chép đi hết hạn chứ không sống mãi như link "public" trước đây.
        string? hlsUrl = null;
        if (userHasAccess && livestream.HlsUrl is { } stored)
        {
            var end = livestream.LoungeShow.ScheduledEnd ?? now.AddHours(3);
            var validUntil = end.AddHours(2) > now.AddHours(2) ? end.AddHours(2) : now.AddHours(2);
            if (validUntil > now.AddHours(24)) validUntil = now.AddHours(24);
            hlsUrl = _streams.GetProvider(livestream.Provider).ViewerPlaybackUrl(stored, validUntil);
        }

        return new LivestreamDetailDto(
            livestream.Id,
            livestream.LoungeShowId,
            livestream.LoungeShow.Name,
            livestream.Status,
            hlsUrl,
            livestream.ViewerCount,
            livestream.StartedAt,
            livestream.EndedAt,
            livestream.TerminatedReason,
            userHasAccess,
            viewingSessionId,
            livestream.IsFree,
            livestream.ChatEnabled);
    }

    // Ngoại lệ CQRS có chủ đích: Query này ghi DB (mở 1 phiên xem mới + cập nhật
    // LivestreamTicketDetail.FirstAccessedAt/LastAccessedAt) thay vì chỉ đọc. Đây là lần gọi ĐẦU
    // TIÊN thành công cấp HlsUrl cho 1 khán giả có vé thật — đúng thời điểm tự nhiên khớp ý nghĩa
    // FirstAccessedAt đã có sẵn trên entity nhưng chưa từng được ghi (guard chặn transfer-vé-đã-xem
    // trong InitiateTicketTransferCommandHandler nhờ vậy lần đầu hoạt động thật). Không tách thành
    // 1 Command riêng vì client cần ViewingSessionId ngay trong response đầu tiên để bắt đầu
    // heartbeat — tách command cho lần mở đầu sẽ buộc client gọi 2 request tuần tự trước khi có thể
    // phát. Các lần giữ phiên sống SAU đó dùng SendLivestreamHeartbeatCommand (Command thật sự).
    private async Task<string> OpenViewingSessionAsync(
        Guid livestreamId, string? previousSessionId, DateTimeOffset now, CancellationToken ct)
    {
        var ticket = await _livestreamRepo.GetViewerTicketAsync(livestreamId, _currentUser.UserId, ct);
        if (ticket is null)
            return string.Empty;

        var maxSessions = await _systemConfig.GetIntAsync(
            ConfigKeys.LivestreamMaxConcurrentSessionsPerTicket, 2, ct);
        var timeoutSeconds = await _systemConfig.GetIntAsync(
            ConfigKeys.LivestreamHeartbeatTimeoutSeconds, 90, ct);
        var cutoff = now.AddSeconds(-timeoutSeconds);

        var sessionRepo = _uow.Repository<LivestreamViewingSession, Guid>();
        // So sanh DateTimeOffset khong dich duoc sang SQL tren SQLite test provider (confirmed
        // thuc nghiem — cung nhom loi da ghi nhan o SumAsync/GetChatMessagesAsync trong file khac).
        // Loc theo TicketId (dich duoc) qua FindAsync, roi so sanh LastHeartbeatAt >= cutoff o C#
        // sau khi da vat chat hoa — giong pattern SubscribeToPackageCommandHandler dang dung cho
        // ExpiresAt.
        var sessionsForTicket = await sessionRepo.FindAsync(s => s.TicketId == ticket.Id, ct);

        // MLACP-513: tải lại trang trên CÙNG trình duyệt dùng lại phiên cũ thay vì mở phiên mới. Trước đây mỗi lần gọi là
        // một phiên mới, phiên cũ còn sống tới hết hạn heartbeat (~90 s) — F5 hai lần là đủ chạm giới hạn 2 thiết bị
        // (fe đo M-441). Chỉ nhận phiên của ĐÚNG vé này, đúng buổi phát này và còn sống; còn lại coi như chưa có phiên.
        if (!string.IsNullOrEmpty(previousSessionId))
        {
            var cu = sessionsForTicket.FirstOrDefault(s =>
                s.SessionId == previousSessionId && s.LivestreamId == livestreamId && s.LastHeartbeatAt >= cutoff);
            if (cu is not null)
            {
                cu.LastHeartbeatAt = now;
                sessionRepo.Update(cu);
                await _uow.SaveChangesAsync(ct);
                return cu.SessionId;
            }
        }

        var activeSessionCount = sessionsForTicket.Count(s => s.LastHeartbeatAt >= cutoff);
        if (activeSessionCount >= maxSessions)
            throw new DomainException(
                $"Vé này đang được xem trên {activeSessionCount} thiết bị — vui lòng đóng bớt trước khi mở thêm.");

        var sessionId = Guid.NewGuid().ToString("N");
        sessionRepo.Add(new LivestreamViewingSession
        {
            TicketId = ticket.Id,
            LivestreamId = livestreamId,
            SessionId = sessionId,
            StartedAt = now,
            LastHeartbeatAt = now
        });

        if (ticket.LivestreamDetail is not null)
        {
            ticket.LivestreamDetail.FirstAccessedAt ??= now;
            ticket.LivestreamDetail.LastAccessedAt = now;
        }

        await _uow.SaveChangesAsync(ct);
        return sessionId;
    }
}
