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
}
