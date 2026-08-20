namespace MusicLounge.Application.Common.Interfaces;

public sealed record GoogleUserInfo(
    string GoogleId,
    string Email,
    string FullName,
    string? AvatarUrl);

public interface IGoogleTokenVerifier
{
    Task<GoogleUserInfo> VerifyAsync(string idToken, CancellationToken ct = default);
}
