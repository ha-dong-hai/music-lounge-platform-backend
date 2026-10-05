using MediatR;
using MusicLounge.Application.Admin.Queries.GetKycReviewQueue;
using MusicLounge.Application.Admin.Queries.GetPayoutAccountReviewQueue;
using MusicLounge.Application.Admin.Queries.GetVenueReviewQueue;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Complaints.Queries.GetComplaintHistory;
using MusicLounge.Application.Moderations;
using MusicLounge.Application.Moderations.Queries.GetContentReportQueue;
using MusicLounge.Application.Moderations.Queries.GetPendingLoungeShows;
using MusicLounge.Application.Refunds;
using MusicLounge.Application.Refunds.Queries.GetPendingRefundRequests;
using MusicLounge.Application.Settlements.Queries.GetSettlementsPendingReview;
using MusicLounge.Application.VenuePenalties.Queries.GetAppealQueue;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Admin.Queries.GetAdminWorkQueue;

/// <summary>
/// MLACP-617. Trang quản trị có 9 hàng đợi việc nhưng menu không cho biết hàng nào đang có việc, việc nào đã trễ hạn.
/// Admin phải mở từng trang để biết — trong khi các job cảnh báo quá hạn đã đang gửi thông báo mà khu Admin không có chuông.
///
/// <b>Số việc chờ</b> lấy bằng cách gửi lại CHÍNH truy vấn danh sách của từng trang (chỉ lấy 1 dòng, đọc
/// <c>TotalCount</c>). Không viết lại điều kiện "đang chờ" ở đây: hai nơi tự định nghĩa cùng một thứ là lớp lỗi đã gặp
/// nhiều lần trong dự án (xem TicketOccupancy, TicketRevenue) — sửa một nơi thì nơi kia lệch, và con số trên menu không
/// còn khớp số dòng khi bấm vào.
///
/// <b>Quá hạn</b> theo đúng mốc của các job cảnh báo đang gửi thông báo cho Admin, nên huy hiệu đỏ và thông báo trong
/// chuông nói cùng một điều:
/// <list type="bullet">
/// <item>Hoàn tiền: tạo lúc + <see cref="RefundSla"/> (72 giờ — Luật BVQLNTD 2023 Điều 31).</item>
/// <item>Báo cáo vi phạm: báo cáo sớm nhất của mỗi nội dung + <see cref="ContentReportSla"/> (48 giờ — NĐ 147/2024).</item>
/// <item>Khiếu nại: cột <c>SlaDeadline</c>. Đếm trên cả Open lẫn Investigating — đúng hàng đợi Admin đang xử lý; job
/// cảnh báo chỉ báo Open (khiếu nại đang điều tra đã có người nhận).</item>
/// <item>Kháng cáo án phạt: cột <c>AppealDeadline</c>.</item>
/// <item>Duyệt buổi diễn: cột <c>EventModeration.SlaDeadline</c> của lượt duyệt chưa có quyết định.</item>
/// </list>
/// Bốn hàng đợi còn lại (phòng trà, định danh, tài khoản nhận tiền, quyết toán chờ duyệt) CHƯA có thời hạn cam kết nào
/// trong hệ thống, nên trả <c>OverdueCount = null</c> chứ không tự đặt một thời hạn không có căn cứ.
///
/// <b>Không có mức "sắp quá hạn":</b> chưa có nguồn nào cho ngưỡng đó; trả <c>NextDueAt</c> để giao diện nói thẳng "còn
/// bao lâu" thay vì một màu dựa trên con số tự đặt.
///
/// Trần giới hạn: 9 truy vấn đếm + 5 truy vấn hạn mỗi lần gọi. Ở quy mô đồ án là rẻ; đường nâng cấp là bộ đếm cập nhật
/// theo sự kiện.
/// </summary>
internal sealed class GetAdminWorkQueueQueryHandler
    : IRequestHandler<GetAdminWorkQueueQuery, IReadOnlyList<AdminWorkQueueItemDto>>
{
    private readonly ISender _sender;
    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;

    public GetAdminWorkQueueQueryHandler(ISender sender, IUnitOfWork uow, ISystemConfigService config)
    {
        _sender = sender;
        _uow = uow;
        _config = config;
    }

    public async Task<IReadOnlyList<AdminWorkQueueItemDto>> Handle(GetAdminWorkQueueQuery request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // ---- số việc chờ: đúng truy vấn của từng trang ----
        var shows = (await _sender.Send(new GetPendingLoungeShowsQuery(1, 1), ct)).TotalCount;
        var venues = (await _sender.Send(new GetVenueReviewQueueQuery(LoungeStatus.Pending, 1, 1), ct)).TotalCount;
        var kyc = (await _sender.Send(new GetKycReviewQueueQuery(KycReviewStatus.Pending, 1, 1), ct)).TotalCount;
        var reports = (await _sender.Send(new GetContentReportQueueQuery(1, 1), ct)).TotalCount;
        var refunds = (await _sender.Send(new GetPendingRefundRequestsQuery(1, 1), ct)).TotalCount;
        var settlements = (await _sender.Send(new GetSettlementsPendingReviewQuery(1, 1), ct)).TotalCount;
        var bankAccounts = (await _sender.Send(new GetPayoutAccountReviewQueueQuery(false, 1, 1), ct)).TotalCount;
        var complaints = (await _sender.Send(
            new GetComplaintHistoryQuery([nameof(ComplaintStatus.Open), nameof(ComplaintStatus.Investigating)], 1, 1), ct)).TotalCount;
        var appeals = (await _sender.Send(new GetAppealQueueQuery(false, 1, 1), ct)).TotalCount;

        // ---- hạn: đúng mốc của các job cảnh báo ----
        var refundSla = await RefundSla.SlaHoursAsync(_config, ct);
        var refundDeadlines = (await _uow.Repository<RefundRequest, Guid>()
                .FindAsync(r => r.Status == RefundRequestStatus.Pending, ct))
            .Select(r => RefundSla.Deadline(new DateTimeOffset(r.CreatedAt, TimeSpan.Zero), refundSla));

        var reportSla = await ContentReportSla.SlaHoursAsync(_config, ct);
        var reportDeadlines = (await _uow.Repository<ContentReport, Guid>()
                .FindAsync(r => r.Status == ContentReportStatus.Open, ct))
            .GroupBy(r => (r.TargetType, r.TargetId))
            .Select(g => g.Min(r => r.CreatedAt).AddHours(reportSla));

        var complaintDeadlines = (await _uow.Repository<Complaint, Guid>()
                .FindAsync(c => (c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.Investigating)
                                && c.SlaDeadline != null, ct))
            .Select(c => c.SlaDeadline!.Value);

        var appealDeadlines = (await _uow.Repository<VenuePenalty, Guid>()
                .FindAsync(p => p.Status == PenaltyStatus.Appealed && p.AppealDeadline != null, ct))
            .Select(p => p.AppealDeadline!.Value);

        var moderationDeadlines = (await _uow.Repository<EventModeration, Guid>()
                .FindAsync(m => m.AdminDecision == null && m.SlaDeadline != null, ct))
            .Select(m => m.SlaDeadline!.Value);

        AdminWorkQueueItemDto CoHan(string key, int count, IEnumerable<DateTimeOffset> han)
        {
            var ds = han.ToList();
            var conHan = ds.Where(d => d > now).ToList();
            return new AdminWorkQueueItemDto(key, count, ds.Count(d => d <= now), conHan.Count > 0 ? conHan.Min() : null);
        }
        AdminWorkQueueItemDto KhongHan(string key, int count) => new(key, count, null, null);

        return
        [
            CoHan("shows", shows, moderationDeadlines),
            KhongHan("venues", venues),
            KhongHan("kyc-reviews", kyc),
            CoHan("content-reports", reports, reportDeadlines),
            CoHan("refunds", refunds, refundDeadlines),
            KhongHan("settlements", settlements),
            KhongHan("bank-accounts", bankAccounts),
            CoHan("complaint", complaints, complaintDeadlines),
            CoHan("penalty-appeals", appeals, appealDeadlines),
        ];
    }
}
