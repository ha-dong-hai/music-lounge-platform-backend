using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Tests.Integration.Fakes;

/// <summary>
/// Test-only stand-in for real Google ID token verification, which requires a live network call
/// to Google's tokeninfo endpoint. Tests build the fake "ID token" as a "|"-delimited
/// "googleId|email|fullName" string instead of a real signed JWT.
/// </summary>
public sealed class FakeGoogleTokenVerifier : IGoogleTokenVerifier
{
    public Task<GoogleUserInfo> VerifyAsync(string idToken, CancellationToken ct = default)
    {
        var parts = idToken.Split('|');
        return Task.FromResult(new GoogleUserInfo(
            GoogleId: parts[0],
            Email: parts[1],
            FullName: parts[2],
            AvatarUrl: null));
    }
}
