using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.BankAccounts.DTOs;

/// <param name="OwnerId">
/// Khoá đa hình: mã PHÒNG TRÀ hoặc mã NGHỆ SĨ tuỳ <paramref name="OwnerType"/> — KHÔNG phải mã người dùng như
/// <c>OwnerId</c> ở mọi chỗ khác trong hệ thống. Giữ lại để không làm vỡ client cũ; client mới dùng
/// <see cref="LoungeId"/> / <see cref="PerformerId"/>.
/// </param>
public sealed record BankAccountDto(
    Guid Id,
    BankAccountOwnerType OwnerType,
    Guid OwnerId,
    string BankName,
    string? AccountNumber,
    string AccountHolder,
    bool IsDefault,
    bool IsVerified,
    /// <summary>MLACP-401. Số tài khoản không còn giải mã được (khoá mã hoá cũ đã mất) — cần nhập lại.</summary>
    bool AccountNumberUnreadable,
    /// <summary>MLACP-668. Có giá trị = Admin đã từ chối và chủ tài khoản chưa sửa lại. <c>IsVerified=false</c> mà trường
    /// này null mới là "đang chờ duyệt"; sửa tài khoản thì trường này về null.</summary>
    DateTimeOffset? RejectedAt = null,
    /// <summary>MLACP-668. Lý do Admin ghi khi từ chối — chủ phòng trà cần biết phải sửa gì.</summary>
    string? RejectionNote = null)
{
    /// <summary>MLACP-454: có giá trị khi đây là tài khoản nhận tiền của phòng trà. Suy ra từ OwnerType + OwnerId nên
    /// không thể lệch với dữ liệu gốc.</summary>
    public Guid? LoungeId => OwnerType == BankAccountOwnerType.Lounge ? OwnerId : null;

    /// <summary>MLACP-454: có giá trị khi đây là tài khoản nhận tiền donate của nghệ sĩ.</summary>
    public Guid? PerformerId => OwnerType == BankAccountOwnerType.Performer ? OwnerId : null;
}
