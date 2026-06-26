using System.ComponentModel.DataAnnotations;

namespace MusicLounge.Application.DTOs.Auth;

public class MReq_VerifyEmail
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string VerificationCode { get; set; } = string.Empty;
}
