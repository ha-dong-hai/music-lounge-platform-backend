namespace MusicLounge.Application.Common.Interfaces;

/// <summary>
/// Encrypts PII columns at rest (currently User.CitizenCardNumber). Unlike ISecretProtector
/// (short-lived Hangfire job arguments), values protected here must stay decryptable for the
/// account's entire lifetime, so this uses its own isolated Data Protection purpose — key
/// rotation or compromise on one never affects the other. Encryption here is non-deterministic
/// (same plaintext produces different ciphertext each time), so equality/uniqueness checks must
/// go through the separate deterministic hash column instead of comparing ciphertext directly.
/// </summary>
public interface IPiiEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);

    /// <summary>
    /// MLACP-401. Như <see cref="Decrypt"/>, nhưng trả null khi giá trị không giải mã được bằng bộ khoá hiện có — ví dụ được mã
    /// hoá bằng khoá đã mất trong lần triển khai 04/09/2026. Chỉ nuốt lỗi mật mã; lỗi khác vẫn ném. Dùng ở chỗ đọc để báo "không
    /// đọc được" thay vì trả 500; chỗ nào tiền phụ thuộc vào giá trị này phải chặn khi nhận null.
    /// </summary>
    string? TryDecrypt(string ciphertext);
}
