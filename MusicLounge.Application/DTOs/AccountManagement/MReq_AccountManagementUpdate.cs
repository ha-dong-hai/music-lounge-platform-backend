using System.ComponentModel.DataAnnotations;

namespace MusicLounge.Application.DTOs.AccountManagement;

public class MReq_AccountManagementUpdate
{
    [Required]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(1000)]
    public string? AvatarUrl { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
