using System;

namespace MusicLounge.Domain.Entities;

public class LoungeStaff
{
    public int Id { get; set; }
    public int LoungeId { get; set; }
    public int UserId { get; set; }
    public DateTime AssignedAt { get; set; }
    public bool IsActive { get; set; }
}

