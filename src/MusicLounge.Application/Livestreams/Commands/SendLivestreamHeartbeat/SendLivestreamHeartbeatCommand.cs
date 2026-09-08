using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Livestreams.Commands.SendLivestreamHeartbeat;

// Client gọi định kỳ (đề xuất 30s/lần) trong lúc đang phát để giữ 1 LivestreamViewingSession
// (mở lần đầu bởi GetLivestreamDetailQuery) còn được tính là "đang hoạt động". Không tự validate
// hạn mức đồng thời ở đây — hạn mức chỉ chặn lúc MỞ phiên mới, không áp lại mỗi lần heartbeat.
public sealed record SendLivestreamHeartbeatCommand(int LivestreamId, string SessionId) : ICommand;
