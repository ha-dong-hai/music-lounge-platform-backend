namespace MusicLounge.Application.FnbMenus.DTOs;

public sealed record FnbMenuDto(
    Guid Id,
    Guid LoungeId,
    string Name,
    string? Description,
    bool IsActive,
    int DisplayOrder);
