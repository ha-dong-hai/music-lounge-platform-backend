using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Security;

// Frontend dang nhap Google qua Firebase Authentication (signInWithPopup + getIdToken()) —
// token nhan duoc la Firebase ID token (issuer https://securetoken.google.com/<project-id>,
// audience = project-id, ky bang key rieng cua Firebase), KHONG PHAI raw Google OAuth ID token
// (issuer accounts.google.com). 2 loai token nay khac han nhau — khong the dung
// GoogleJsonWebSignature (thu vien Google.Apis.Auth, danh cho raw Google token) de verify duoc
// token Firebase phat ra, du co cau hinh dung Client ID di nua. Xac minh thu cong bang JWKS
// cong khai cua Firebase, khong can service-account credential (verify chu ky la thao tac public).
internal sealed class GoogleTokenVerifier : IGoogleTokenVerifier
{
    private const string JwksUrl = "https://www.googleapis.com/service_accounts/v1/jwk/securetoken@system.gserviceaccount.com";
    private const string JwksCacheKey = "firebase:jwks";

    private readonly FirebaseSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public GoogleTokenVerifier(
        IOptions<FirebaseSettings> settings, IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task<GoogleUserInfo> VerifyAsync(string idToken, CancellationToken ct = default)
    {
        var signingKeys = await GetSigningKeysAsync(ct);

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = $"https://securetoken.google.com/{_settings.ProjectId}",
            ValidAudience = _settings.ProjectId,
            IssuerSigningKeys = signingKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };

        ClaimsPrincipal principal;
        try
        {
            // MapInboundClaims=false — mac dinh JwtSecurityTokenHandler doi ten claim ngan (vd
            // "email", "sub", "name") sang URI dai kieu WS-Federation cu
            // (http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress) de tuong thich
            // nguoc, khien FindFirst("email") ben duoi luon tra ve null du token hop le. Tat mapping
            // nay de giu dung ten claim goc nhu Firebase phat hanh.
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            principal = handler.ValidateToken(idToken, validationParameters, out _);
        }
        catch (SecurityTokenException)
        {
            throw new UnauthorizedException("Google ID token không hợp lệ hoặc đã hết hạn.");
        }

        // Google khuyen cao ro: phai tu kiem tra email_verified truoc khi tin claim email de
        // lien ket tai khoan — bo qua buoc nay se cho phep GoogleLoginCommandHandler tu dong
        // gan Google identity vao 1 tai khoan local co san chi dua tren email chua chac da
        // duoc Google xac minh that su.
        var emailVerified = principal.FindFirst("email_verified")?.Value;
        if (!string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedException("Email Google chưa được xác minh.");

        var subject = principal.FindFirst("user_id")?.Value ?? principal.FindFirst("sub")?.Value
            ?? throw new UnauthorizedException("Google ID token thiếu định danh người dùng.");
        var email = principal.FindFirst("email")?.Value
            ?? throw new UnauthorizedException("Google ID token thiếu email.");
        var name = principal.FindFirst("name")?.Value ?? email;
        var picture = principal.FindFirst("picture")?.Value;

        return new GoogleUserInfo(subject, email, name, picture);
    }

    private async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken ct)
    {
        var cached = await _cache.GetOrCreateAsync(JwksCacheKey, async entry =>
        {
            var client = _httpClientFactory.CreateClient("firebase");
            using var response = await client.GetAsync(JwksUrl, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);

            // Google JWKS thay doi key rat hiem (xoay vong dinh ky), nhung van dat TTL ngan de
            // khong bao gio phuc vu key da bi thu hoi qua lau — khong doc Cache-Control header
            // cua response de giu logic don gian, du co the fetch hoi thua so voi max-age that.
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return new JsonWebKeySet(json).Keys.Cast<SecurityKey>().ToList();
        });

        return cached ?? throw new UnauthorizedException("Không thể tải khoá xác minh Google.");
    }
}
