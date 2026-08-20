using Microsoft.AspNetCore.DataProtection;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

internal sealed class DataProtectionSecretProtector : ISecretProtector
{
    private const string Purpose = "MusicLounge.HangfireJobArgs.v1";
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector(Purpose);

    public string Protect(string plaintext) => _protector.Protect(plaintext);
    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
