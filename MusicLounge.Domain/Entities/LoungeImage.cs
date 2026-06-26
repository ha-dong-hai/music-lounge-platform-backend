using System;

namespace MusicLounge.Domain.Entities;

public class LoungeImage
{
    public int Id { get; set; }
    public int LoungeId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

