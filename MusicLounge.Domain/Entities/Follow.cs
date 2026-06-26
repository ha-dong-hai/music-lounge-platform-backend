using System;

namespace MusicLounge.Domain.Entities;

public class Follow
{
    public int UserId { get; set; }
    public int ArtistId { get; set; }
}

