using System.ComponentModel.DataAnnotations;

namespace MusicLounge.Application.DTOs.Auth;

public class MReq_Login
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
