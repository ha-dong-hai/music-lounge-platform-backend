namespace MusicLounge.Application.FnbMenus.DTOs;

public sealed record FnbMenuDto(
    int Id,
    int LoungeId,
    string Name,
    string? Description,
    bool IsActive,
    int DisplayOrder);
