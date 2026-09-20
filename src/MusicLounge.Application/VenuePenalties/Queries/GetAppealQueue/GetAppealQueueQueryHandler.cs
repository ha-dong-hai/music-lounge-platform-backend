using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.VenuePenalties.DTOs;
using MusicLounge.Domain.Entities;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.VenuePenalties.Queries.GetAppealQueue;

internal sealed class GetAppealQueueQueryHandler
    : IRequestHandler<GetAppealQueueQuery, PaginatedResult<VenuePenaltyDto>>
{
    private readonly IUnitOfWork _uow;

    public GetAppealQueueQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<PaginatedResult<VenuePenaltyDto>> Handle(
        GetAppealQueueQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);

        // "Đang chờ" = đã kháng nghị mà chưa có quyết định. ReviewedAt là dấu của quyết định, không
        // phải AppealResult: AppealResult chỉ được gán khi có quyết định, nhưng đọc theo ReviewedAt
        // thì cùng một câu hỏi với lệnh xử lý kháng nghị đang kiểm.
        //
        // Sắp theo Id chứ không theo AppealedAt: trình cung cấp SQLite dùng trong test không dịch
        // được ORDER BY trên DateTimeOffset — cùng hạn chế mà GetMyVenuePenaltiesQueryHandler đã ghi.
        // Ở đây Id không đồng nghĩa với thứ tự kháng nghị (một án phạt cũ có thể vừa được kháng nghị),
        // nên trang được sắp lại theo AppealedAt sau khi lấy về, còn phân trang vẫn ở phía máy chủ.
        var (pageItems, totalCount) = request.Resolved
            ? await _uow.Repository<VenuePenalty, int>()
                .GetPagedAsync(p => p.AppealedAt != null && p.ReviewedAt != null, p => p.Id, page, size, ct)
            : await _uow.Repository<VenuePenalty, int>()
                .GetPagedAsync(p => p.AppealedAt != null && p.ReviewedAt == null, p => p.Id, page, size, ct);

        var loungeIds = pageItems.Select(p => p.LoungeId).Distinct().ToList();
        var loungeNames = (await _uow.Repository<MusicLoungeEntity, int>()
                .FindAsync(l => loungeIds.Contains(l.Id), ct))
            .ToDictionary(l => l.Id, l => l.Name);

        var items = pageItems
            .OrderBy(p => p.AppealedAt)   // cũ nhất trước, trong phạm vi trang
            .Select(p => new VenuePenaltyDto(
                p.Id, p.LoungeId, loungeNames.GetValueOrDefault(p.LoungeId, string.Empty),
                p.PenaltyType, p.Reason, p.EvidenceRef,
                p.IssuedAt, p.EffectiveAt, p.SuspensionDays, p.SuspensionEnd, p.Status,
                p.AppealDeadline, p.AppealedAt, p.AppealReason, p.AppealResult, p.ReviewedAt))
            .ToList();

        return new PaginatedResult<VenuePenaltyDto>(items, page, size, totalCount);
    }
}
