namespace MusicLounge.Application.FnbMenuItems.DTOs;

public sealed record FnbMenuItemDto(
    int Id,
    int MenuId,
    string Category,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsAvailable,
    int DisplayOrder);
