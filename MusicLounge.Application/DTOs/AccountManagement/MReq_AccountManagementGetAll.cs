namespace MusicLounge.Application.DTOs.AccountManagement;

public class MReq_AccountManagementGetAll
{
    public string? SearchText { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsEmailVerified { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
