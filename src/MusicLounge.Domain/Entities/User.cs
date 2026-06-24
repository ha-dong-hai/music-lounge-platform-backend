using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class User : AuditableEntity<int>
{
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public UserRole Role { get; set; }
    public string AuthProvider { get; set; } = "email";
    public string? GoogleId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool AiConsent { get; set; } = false;
}
