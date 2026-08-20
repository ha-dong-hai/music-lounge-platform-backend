using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.BankAccounts.DTOs;

public sealed record BankAccountDto(
    int Id,
    BankAccountOwnerType OwnerType,
    int OwnerId,
    string BankName,
    string AccountNumber,
    string AccountHolder,
    bool IsDefault,
    bool IsVerified);
