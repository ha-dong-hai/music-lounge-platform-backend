using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Hubs;

/// <summary>
/// MLACP-669. Kênh "dữ liệu của bạn vừa đổi" cho web. Trình duyệt chỉ NGHE, không gọi phương thức nào: sự kiện do
/// <c>RealtimeOutbox</c> phát sau khi commit, qua <c>SignalRRealtimeNotifier</c>.
///
/// <para>Nhóm do máy chủ gán theo JWT, người dùng không tự chọn được: <c>user:{id}</c> cho chính mình, và <c>admins</c>
/// nếu là Admin. Sự kiện chỉ mang chủ đề + mã tham chiếu; nhận xong web gọi lại API, nên quyền đọc vẫn do API kiểm.</para>
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public const string AdminsGroup = "admins";

    private readonly ICurrentUserService _currentUser;

    public NotificationHub(ICurrentUserService currentUser) => _currentUser = currentUser;

    public static string UserGroup(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        if (!_currentUser.IsAuthenticated) { Context.Abort(); return; }
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(_currentUser.UserId));
        if (_currentUser.Role == Roles.Admin)
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
        await base.OnConnectedAsync();
    }
}
