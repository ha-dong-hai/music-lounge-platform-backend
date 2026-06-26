using System.ComponentModel.DataAnnotations;

namespace MusicLounge.Application.DTOs.Auth;

public class MReq_UpdateCitizenCard
{
    [Required]
    [MaxLength(50)]
    public string CitizenCardNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string CitizenCardFrontImageUrl { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string CitizenCardBackImageUrl { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? StorageProvider { get; set; } = "Firebase";
}
