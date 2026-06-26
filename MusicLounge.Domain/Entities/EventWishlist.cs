using System;

namespace MusicLounge.Domain.Entities;

public class EventWishlist
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int EventId { get; set; }
    public DateTime SavedAt { get; set; }
}

