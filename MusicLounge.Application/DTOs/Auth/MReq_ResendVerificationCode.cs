using System.ComponentModel.DataAnnotations;

namespace MusicLounge.Application.DTOs.Auth;

public class MReq_ResendVerificationCode
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
