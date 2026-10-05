using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Infrastructure.Realtime;

/// <summary>
/// MLACP-669. Gom sự kiện thời gian thực từ chính những gì vừa được lưu, rồi phát khi dữ liệu đã thật sự nằm trong DB.
///
/// <para><b>Vì sao suy từ ChangeTracker chứ không gọi ở từng handler:</b> hệ thống có ~80 chỗ gọi
/// <c>INotificationService.NotifyAsync</c> và hàng chục lệnh đổi trạng thái các hàng đợi duyệt. Gọi tay ở từng nơi thì nơi
/// nào quên là màn đó lại "không cập nhật" — đúng loại lỗi đang sửa. Một <c>Notification</c> được thêm, hay một bản ghi
/// thuộc hàng đợi Admin được thêm/sửa/xoá, là đủ để biết ai cần tải lại.</para>
///
/// <para><b>Thời điểm phát:</b> <c>ApplicationDbContext.SaveChangesAsync</c> gom; nếu không có transaction thì phát ngay sau
/// khi lưu, nếu đang trong transaction (<c>TransactionBehavior</c> → <c>UnitOfWork</c>) thì giữ lại tới
/// <c>CommitTransactionAsync</c>, rollback thì bỏ. Lưu hỏng thì không gom gì (gom vào danh sách tạm, chỉ nhận khi lưu xong).</para>
///
/// <para><b>Trần giới hạn:</b> phát trong tiến trình qua SignalR — đúng khi API chạy MỘT phiên bản (Azure App Service B1
/// hiện tại). Nâng lên nhiều phiên bản thì cần backplane (Azure SignalR Service hoặc Redis), nếu không người dùng nối vào
/// phiên bản A sẽ không nhận sự kiện phát từ B. Mất kết nối thì web vẫn tự tải lại khi quay lại tab — kênh này chỉ làm
/// nhanh hơn, không phải nguồn sự thật.</para>
/// </summary>
public sealed class RealtimeOutbox
{
    // Khoá trùng với GetAdminWorkQueueQueryHandler — web dùng một bảng ánh xạ cho số đếm menu lẫn danh sách.
    private static readonly Dictionary<Type, string> AdminQueueOf = new()
    {
        [typeof(LoungeShow)] = "shows",
        [typeof(EventModeration)] = "shows",
        [typeof(MusicLoungeEntity)] = "venues",
        [typeof(ContentReport)] = "content-reports",
        [typeof(RefundRequest)] = "refunds",
        [typeof(Settlement)] = "settlements",
        [typeof(BankAccount)] = "bank-accounts",
        [typeof(Complaint)] = "complaint",
        [typeof(VenuePenalty)] = "penalty-appeals",
    };

    // Chỉ các cột thuộc hàng đợi duyệt định danh. User bị sửa ở rất nhiều đường (đăng nhập ghi mốc, đổi hồ sơ…); báo cho
    // Admin mỗi lần như vậy là ồn vô ích.
    private static readonly string[] KycColumns =
    [
        nameof(User.CitizenCardSubmittedAt), nameof(User.CitizenCardReviewStatus),
        nameof(User.TaxProfileSubmittedAt), nameof(User.TaxProfileReviewStatus),
    ];

    private readonly IRealtimeNotifier _notifier;
    private readonly List<RealtimeEvent> _pending = [];

    public RealtimeOutbox(IRealtimeNotifier notifier) => _notifier = notifier;

    /// <summary>Đọc ChangeTracker TRƯỚC khi lưu (sau khi lưu mọi entry đều về Unchanged).</summary>
    public static List<RealtimeEvent> Collect(ChangeTracker tracker)
    {
        var events = new List<RealtimeEvent>();
        foreach (var entry in tracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            if (entry.Entity is Notification n && entry.State == EntityState.Added)
            {
                events.Add(new RealtimeEvent(n.UserId, "notification", n.ReferenceType, n.ReferenceId));
                continue;
            }

            // So GIÁ TRỊ, không dùng IsModified: Repository.Update() đánh dấu mọi cột là đã sửa, nên sửa họ tên cũng sẽ
            // "chạm" cột định danh (đo bằng EditingAnOrdinaryProfile_DoesNotPingTheKycQueue).
            if (entry.Entity is User && entry.State == EntityState.Modified
                && KycColumns.Any(c => !Equals(entry.Property(c).OriginalValue, entry.Property(c).CurrentValue)))
            {
                events.Add(new RealtimeEvent(null, "kyc-reviews"));
                continue;
            }

            // Tài khoản của nghệ sĩ do nghệ sĩ tự xác nhận, không nằm trong hàng đợi Admin.
            if (entry.Entity is BankAccount { OwnerType: not BankAccountOwnerType.Lounge }) continue;

            if (AdminQueueOf.TryGetValue(entry.Entity.GetType(), out var queue))
                events.Add(new RealtimeEvent(null, queue));
        }
        return events;
    }

    public void Stage(IEnumerable<RealtimeEvent> events) => _pending.AddRange(events);

    public void Discard() => _pending.Clear();

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_pending.Count == 0) return;
        var batch = _pending.Distinct().ToList();
        _pending.Clear();
        await _notifier.PublishAsync(batch, ct);
    }
}
