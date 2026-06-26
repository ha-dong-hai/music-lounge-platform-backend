using System;

namespace MusicLounge.Domain.Entities;

public class UserBehaviourLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int EventId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

