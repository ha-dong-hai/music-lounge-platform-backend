namespace MusicLounge.Domain.Enums;

/// <summary>MLACP-364 — nghệ sĩ được mời xác nhận điều gì qua liên kết một lần.</summary>
public enum PerformerConfirmationPurpose
{
    BankAccount,      // tài khoản ngân hàng phòng trà nhập cho nghệ sĩ là của chính nghệ sĩ
    DonationReceipt   // đã nhận khoản donate phòng trà báo đã chuyển
}

/// <summary>MLACP-364 — nghệ sĩ trả lời thế nào.</summary>
public enum PerformerConfirmationOutcome
{
    Confirmed,
    Disputed
}
