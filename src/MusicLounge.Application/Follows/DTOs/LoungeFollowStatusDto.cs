namespace MusicLounge.Application.Follows.DTOs;

/// <summary>MLACP-503. Người đang đăng nhập có theo dõi phòng trà này không.</summary>
public sealed record LoungeFollowStatusDto(int LoungeId, bool IsFollowing);
