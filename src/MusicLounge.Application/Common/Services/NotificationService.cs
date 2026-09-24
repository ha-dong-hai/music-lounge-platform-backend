using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Services;

internal sealed class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IBackgroundJobService _jobs;

    public NotificationService(IUnitOfWork uow, IBackgroundJobService jobs)
    {
        _uow = uow;
        _jobs = jobs;
    }

    // Stages the row via Add() only — like ILedgerService.WriteJournalAsync, the caller's own
    // SaveChangesAsync (already required at the end of every handler/job) commits it, so a
    // notification never persists half-committed relative to the change that triggered it.
    public async Task NotifyAsync(
        int userId,
        NotificationType type,
        SongNgu title,
        SongNgu body,
        string? referenceType = null,
        string? referenceId = null,
        CancellationToken ct = default)
    {
        // MLACP-489: cắt ở đây, một chỗ, thay vì tin 81 chỗ gọi. Câu có chèn nội dung do người gõ (ghi chú của Admin,
        // mô tả khiếu nại, lý do phạt) nên dài không lường trước; vượt độ dài cột thì SQL Server ném lỗi lúc lưu, mà
        // nhiều chỗ gọi nằm trong transaction thanh toán — CLAUDE.md: không throw bên trong transaction thanh toán.
        // Rủi ro này có từ trước với bản tiếng Việt; thêm cột tiếng Anh mà không chặn là thêm một đường hỏng nữa.
        var titleVi = Cat(title.Vi, NotificationLimits.TitleMaxLength);
        var bodyVi = Cat(body.Vi, NotificationLimits.BodyMaxLength);
        var titleEn = Cat(title.En, NotificationLimits.TitleMaxLength);
        var bodyEn = Cat(body.En, NotificationLimits.BodyMaxLength);

        _uow.Repository<Notification, int>().Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = titleVi,
            Body = bodyVi,
            TitleEn = titleEn,
            BodyEn = bodyEn,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Push đi bất đồng bộ nên theo ngôn ngữ lưu trên tài khoản NGƯỜI NHẬN, không theo request đang chạy — request
        // đó thường là của người khác (Admin duyệt phòng trà → báo chủ phòng trà). Xem IRequestLanguage.
        //
        // Chọn ngôn ngữ ở đây (lúc enqueue) chứ không trong job push, để chữ ký job FCM giữ nguyên: job Hangfire đã
        // xếp hàng lưu tên phương thức + kiểu tham số, đổi chữ ký thì các job đang chờ lúc deploy sẽ không chạy được.
        //
        // TRẦN: mỗi thông báo một lần đọc User theo khoá chính (thường đã nằm trong change tracker nên không tốn truy
        // vấn). Buổi hòa nhạc bị huỷ với N vé là N lần đọc — ổn ở quy mô hiện tại. Nâng cấp khi cần: nạp sẵn
        // PreferredLanguage theo lô cho danh sách người nhận rồi truyền vào.
        var recipient = await _uow.Repository<User, int>().GetByIdAsync(userId, ct);
        var lang = recipient?.PreferredLanguage;
        var pushTitle = NgonNgu.LaTiengAnh(lang) && !string.IsNullOrWhiteSpace(titleEn) ? titleEn : titleVi;
        var pushBody = NgonNgu.LaTiengAnh(lang) && !string.IsNullOrWhiteSpace(bodyEn) ? bodyEn : bodyVi;

        _jobs.EnqueueFcmNotification(userId, pushTitle, pushBody, referenceType, referenceId);
    }

    private static string Cat(string text, int max)
        => text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");
}
