using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Moderations.Commands.ReviewTicketTier;

/// <summary>MLACP-388: Admin duyệt hạng vé livestream được thêm sau khi buổi diễn đã đăng.</summary>
public sealed record ReviewTicketTierCommand(
    int TierId,
    string Decision,
    string? ReviewNote
) : ICommand;
