using System;

namespace MusicLounge.Application.DTOs.Auth;

public class MRes_Auth : MRes_UserProfile
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
