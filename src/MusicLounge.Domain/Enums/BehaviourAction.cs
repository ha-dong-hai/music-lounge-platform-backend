namespace MusicLounge.Domain.Enums;

public enum BehaviourAction
{
    ViewEvent,
    ViewEventLong,      // dwell > threshold
    ViewLineup,
    ViewVenue,
    SearchGenre,
    WatchLivestream,
    ShareEvent,
    ClickTicket,
    ViewAfterWishlist,
    PurchaseTicket      // MLACP-133: ve da duoc xac nhan thanh toan thanh cong
}
