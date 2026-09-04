using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Livestreams.Commands.CreateLivestream;

// IsFree defaults to false (paid/ticket-gated) — a livestream exists on this platform specifically
// to sell Livestream-access tickets (TicketTier.AccessType.Livestream); defaulting new streams to
// "free for anyone" would silently bypass that revenue path for every show unless the Owner
// explicitly opts in. GetLivestreamDetailQueryHandler/GetChatHistoryQueryHandler already gate on
// this flag correctly (MLACP-117/MLACP-119) — the bug was only ever in this default.
public sealed record CreateLivestreamCommand(int ShowId, bool IsFree = false, bool ChatEnabled = true)
    : ICommand<int>;
