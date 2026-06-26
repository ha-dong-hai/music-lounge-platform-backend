using System.ComponentModel.DataAnnotations;

namespace MusicLounge.Application.DTOs.Auth;

public class MReq_UpdateProfile
{
    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(1000)]
    public string? AvatarUrl { get; set; }
}
