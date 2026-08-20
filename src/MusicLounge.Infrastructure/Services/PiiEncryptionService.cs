using Microsoft.AspNetCore.DataProtection;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

internal sealed class PiiEncryptionService : IPiiEncryptionService
{
    private const string Purpose = "MusicLounge.PiiAtRest.v1";
    private readonly IDataProtector _protector;

    public PiiEncryptionService(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector(Purpose);

    public string Encrypt(string plaintext) => _protector.Protect(plaintext);
    public string Decrypt(string ciphertext) => _protector.Unprotect(ciphertext);
}
