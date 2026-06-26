using System;

namespace MusicLounge.Domain.Entities;

public class UserFavoriteGenre
{
    public int UserId { get; set; }
    public int GenreId { get; set; }
}

