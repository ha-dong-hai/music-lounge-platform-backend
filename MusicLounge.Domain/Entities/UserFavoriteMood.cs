using System;

namespace MusicLounge.Domain.Entities;

public class UserFavoriteMood
{
    public int UserId { get; set; }
    public int MoodId { get; set; }
}

