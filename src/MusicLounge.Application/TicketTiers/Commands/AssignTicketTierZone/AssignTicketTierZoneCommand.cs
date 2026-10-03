using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.TicketTiers.Commands.AssignTicketTierZone;

// MLACP-545: gắn khu ghế cho một hạng vé TẠI CHỖ. Tách khỏi UpdateTicketTier vì lệnh đó khoá mọi thứ sau khi buổi diễn
// rời Draft (giá, sức chứa) — còn khu thì được gắn MỘT lần kể cả khi đã mở bán, xem handler.
public sealed record AssignTicketTierZoneCommand(Guid TierId, Guid ZoneId) : ICommand;
