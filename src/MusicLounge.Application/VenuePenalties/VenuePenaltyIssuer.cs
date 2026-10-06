using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.VenuePenalties;

/// <summary>
/// MLACP-676. Ra một án phạt cho phòng trà — tách khỏi <c>IssuePenaltyCommandHandler</c> để lệnh Admin xét lý do huỷ buổi
/// (<c>DecideShowCancellationReviewCommand</c>) ra án theo ĐÚNG luật đó (độ trễ hiệu lực theo mức phạt, trạng thái phòng trà,
/// thông báo cho chủ), thay vì chép lại hoặc gửi lồng một lệnh MediatR thứ hai trong cùng giao dịch (lỗi đã gặp ở
/// ComplaintResolvedAction).
///
/// <para>Chỉ Add vào unit of work — người gọi tự lưu.</para>
/// </summary>
public static class VenuePenaltyIssuer
{
    public static async Task<VenuePenalty> IssueAsync(
        IUnitOfWork uow, INotificationService notifications, ISystemConfigService config, MusicLoungeEntity lounge,
        PenaltyType penaltyType, string reason, string? evidenceRef, int? suspensionDays, Guid issuedBy, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // §6.8 — each severity takes effect on its own delay, giving the Owner notice before it
        // bites: warning is immediate, suspension +notice hours, ban +notice days.
        // ApplyDuePenaltiesJob applies the venue-status change and subscription compensation once
        // EffectiveAt arrives.
        var suspensionNoticeHours = await config.GetIntAsync(ConfigKeys.PenaltySuspensionNoticeHours, 24, ct);
        var banNoticeDays = await config.GetIntAsync(ConfigKeys.PenaltyBanNoticeDays, 7, ct);
        var effectiveAt = penaltyType switch
        {
            PenaltyType.Warning => now,
            PenaltyType.Suspension => now.AddHours(suspensionNoticeHours),
            PenaltyType.Ban => now.AddDays(banNoticeDays),
            _ => now
        };

        var penalty = new VenuePenalty
        {
            LoungeId = lounge.Id,
            PenaltyType = penaltyType,
            Reason = reason,
            EvidenceRef = evidenceRef,
            IssuedBy = issuedBy,
            IssuedAt = now,
            EffectiveAt = effectiveAt,
            SuspensionDays = penaltyType == PenaltyType.Suspension ? suspensionDays : null,
            Status = PenaltyStatus.Active
        };
        uow.Repository<VenuePenalty, Guid>().Add(penalty);

        // Warning has no delay and no venue-status/subscription effect (§6.8: "venue vẫn hoạt
        // động, subscription không đổi") — apply it here rather than waiting for the job.
        //
        // MLACP-367: truoc day dat thang Warned — ke ca khi phong tra dang bi tam khoa/khoa vinh vien (Warned
        // van duoc hoat dong, nen canh cao vo tinh mo khoa) hoac chua duoc duyet ho so.
        if (penaltyType == PenaltyType.Warning
            && PenaltyLifecycle.StatusAfterImposing(lounge.Status, penaltyType) is { } warnedStatus)
        {
            lounge.Status = warnedStatus;
            uow.Repository<MusicLoungeEntity, Guid>().Update(lounge);
        }

        await notifications.NotifyAsync(
            lounge.OwnerId,
            NotificationType.PenaltyIssued,
            new SongNgu(
                penaltyType == PenaltyType.Warning ? "Phòng trà bị cảnh cáo" : "Phòng trà bị xử phạt",
                penaltyType == PenaltyType.Warning
                    ? "Your music lounge has received a warning"
                    : "Your music lounge has been penalised"),
            new SongNgu(
                penaltyType switch
                {
                    PenaltyType.Warning => $"\"{lounge.Name}\" nhận cảnh cáo: {reason}",
                    PenaltyType.Suspension => $"\"{lounge.Name}\" sẽ bị tạm khoá {suspensionDays} ngày " +
                        $"kể từ {VietnamTime.Format(effectiveAt)}. Lý do: {reason}. Bạn có thể kháng cáo.",
                    PenaltyType.Ban => $"\"{lounge.Name}\" sẽ bị khoá vĩnh viễn kể từ {VietnamTime.Format(effectiveAt)}. " +
                        $"Lý do: {reason}. Bạn có thể kháng cáo.",
                    _ => reason
                },
                penaltyType switch
                {
                    PenaltyType.Warning => $"\"{lounge.Name}\" has received a warning: {reason}",
                    PenaltyType.Suspension => $"\"{lounge.Name}\" will be suspended for {suspensionDays} days " +
                        $"from {VietnamTime.Format(effectiveAt)}. Reason: {reason}. You can appeal.",
                    PenaltyType.Ban => $"\"{lounge.Name}\" will be permanently banned from {VietnamTime.Format(effectiveAt)}. " +
                        $"Reason: {reason}. You can appeal.",
                    _ => reason
                }),
            referenceType: "venue_penalty",
            referenceId: penalty.Id.ToString(),
            ct: ct);

        return penalty;
    }
}
