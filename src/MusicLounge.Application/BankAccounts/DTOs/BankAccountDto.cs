using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.BankAccounts.DTOs;

/// <param name="OwnerId">
/// Khoá đa hình: mã PHÒNG TRÀ hoặc mã NGHỆ SĨ tuỳ <paramref name="OwnerType"/> — KHÔNG phải mã người dùng như
/// <c>OwnerId</c> ở mọi chỗ khác trong hệ thống. Giữ lại để không làm vỡ client cũ; client mới dùng
/// <see cref="LoungeId"/> / <see cref="PerformerId"/>.
/// </param>
public sealed record BankAccountDto(
    int Id,
    BankAccountOwnerType OwnerType,
    int OwnerId,
    string BankName,
    string? AccountNumber,
    string AccountHolder,
    bool IsDefault,
    bool IsVerified,
    /// <summary>MLACP-401. Số tài khoản không còn giải mã được (khoá mã hoá cũ đã mất) — cần nhập lại.</summary>
    bool AccountNumberUnreadable)
{
    /// <summary>MLACP-454: có giá trị khi đây là tài khoản nhận tiền của phòng trà. Suy ra từ OwnerType + OwnerId nên
    /// không thể lệch với dữ liệu gốc.</summary>
    public int? LoungeId => OwnerType == BankAccountOwnerType.Lounge ? OwnerId : null;

    /// <summary>MLACP-454: có giá trị khi đây là tài khoản nhận tiền donate của nghệ sĩ.</summary>
    public int? PerformerId => OwnerType == BankAccountOwnerType.Performer ? OwnerId : null;
}
