using System;

namespace MusicLounge.Application.DTOs.Auth;

public class MRes_Register
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsVerificationRequired { get; set; }
    public DateTime? VerificationCodeExpiredAt { get; set; }
}
