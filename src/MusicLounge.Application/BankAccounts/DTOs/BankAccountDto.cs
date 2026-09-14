using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.BankAccounts.DTOs;

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
    bool AccountNumberUnreadable);
