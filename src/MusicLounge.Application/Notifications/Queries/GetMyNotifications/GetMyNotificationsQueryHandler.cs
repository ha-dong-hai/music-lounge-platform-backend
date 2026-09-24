using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Notifications.DTOs;

namespace MusicLounge.Application.Notifications.Queries.GetMyNotifications;

internal sealed class GetMyNotificationsQueryHandler
    : IRequestHandler<GetMyNotificationsQuery, PaginatedResult<NotificationDto>>
{
    private readonly INotificationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IRequestLanguage _language;

    public GetMyNotificationsQueryHandler(
        INotificationRepository repo, ICurrentUserService currentUser, IRequestLanguage language)
    {
        _repo = repo;
        _currentUser = currentUser;
        _language = language;
    }

    public async Task<PaginatedResult<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);
        // MLACP-489: danh sách trả trong response nên theo Accept-Language của chính request này — người vừa đổi ngôn
        // ngữ trên giao diện thấy ngay, không phụ thuộc cài đặt đã lưu. (Push thì theo cài đặt đã lưu — xem IRequestLanguage.)
        return await _repo.GetMyNotificationsAsync(_currentUser.UserId, page, size, _language.Current, ct);
    }
}
