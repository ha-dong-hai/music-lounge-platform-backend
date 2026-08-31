using MediatR;
using MusicLounge.Application.Analytics.Common;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerRevenueReport;

internal sealed class GetOwnerRevenueReportQueryHandler
    : IRequestHandler<GetOwnerRevenueReportQuery, OwnerRevenueReportDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IOwnerRevenueReportBuilder _builder;

    public GetOwnerRevenueReportQueryHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IOwnerRevenueReportBuilder builder)
    {
        _uow = uow;
        _currentUser = currentUser;
        _builder = builder;
    }

    public async Task<OwnerRevenueReportDto> Handle(GetOwnerRevenueReportQuery request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xem báo cáo doanh thu của venue này.");

        return await _builder.BuildAsync(request.LoungeId, request.From, request.To, ct);
    }
}
