using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Security;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.Auth;

/// <summary>
/// MLACP-412. Bộ kiểm token thật chưa có test nào: mọi test đăng nhập Google đều thay nó bằng bản giả
/// (<c>FakeGoogleTokenVerifier</c>). Trên Azure, gửi <c>POST /auth/google</c> với token sai định dạng nhận **500**
/// "An unexpected error occurred" thay vì 401 — thư viện JWT ném loại exception không nằm trong nhánh
/// <c>SecurityTokenException</c> mà bộ kiểm đang bắt. Test dựng thẳng bộ kiểm với bộ khoá tự tạo (không gọi mạng)
/// nên chạy được cả nhánh token hợp lệ.
/// </summary>
public sealed class GoogleTokenVerifierTests
{
    private const string ProjectId = "sign-in-52d07";
    private const string KeyId = "test-key-1";

    private static readonly RSA Rsa = RSA.Create(2048);

    private sealed class KhongGoiMangHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException("Bộ khoá đã nằm sẵn trong cache — test không được gọi ra mạng.");
    }

    private static GoogleTokenVerifier TaoBoKiem()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("firebase:jwks", new List<SecurityKey> { new RsaSecurityKey(Rsa) { KeyId = KeyId } });
        return new GoogleTokenVerifier(
            Options.Create(new FirebaseSettings { ProjectId = ProjectId }),
            new KhongGoiMangHttpClientFactory(),
            cache);
    }

    private static string TaoToken(
        string? issuer = null, string? audience = null, bool emailVerified = true, string? email = "nhanvien@gmail.com")
    {
        var creds = new SigningCredentials(new RsaSecurityKey(Rsa) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256);
        var claims = new List<Claim>
        {
            new("sub", "google-uid-123"),
            new("name", "Hà Đông Hải"),
            new("email_verified", emailVerified ? "true" : "false"),
        };
        if (email is not null) claims.Add(new Claim("email", email));

        var token = new JwtSecurityToken(
            issuer: issuer ?? $"https://securetoken.google.com/{ProjectId}",
            audience: audience ?? ProjectId,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Theory]
    [InlineData("abc.def.ghi")]            // đúng 3 phần nhưng không phải base64 JWT — chính là ca gây 500 trên Azure
    [InlineData("khong-phai-jwt")]         // không có dấu chấm nào
    [InlineData("")]                       // rỗng
    [InlineData("a.b")]                    // thiếu phần chữ ký
    public async Task TokenSaiDinhDang_TraLoiDangNhapThatBai_KhongPhaiLoiHeThong(string idToken)
    {
        var act = () => TaoBoKiem().VerifyAsync(idToken);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Google ID token không hợp lệ hoặc đã hết hạn.");
    }

    [Fact]
    public async Task TokenKyBangKhoaKhac_TraLoiDangNhapThatBai()
    {
        var khoaLa = RSA.Create(2048);
        var creds = new SigningCredentials(new RsaSecurityKey(khoaLa) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: $"https://securetoken.google.com/{ProjectId}", audience: ProjectId,
            expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: creds));

        var act = () => TaoBoKiem().VerifyAsync(token);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task TokenCuaDuAnKhac_TraLoiDangNhapThatBai()
    {
        var act = () => TaoBoKiem().VerifyAsync(TaoToken(issuer: "https://securetoken.google.com/du-an-khac",
            audience: "du-an-khac"));

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task EmailChuaDuocGoogleXacMinh_TraLoiDangNhapThatBai()
    {
        var act = () => TaoBoKiem().VerifyAsync(TaoToken(emailVerified: false));

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Email Google chưa được xác minh.");
    }

    [Fact]
    public async Task TokenHopLe_TraVeThongTinNguoiDung()
    {
        var info = await TaoBoKiem().VerifyAsync(TaoToken());

        info.GoogleId.Should().Be("google-uid-123");
        info.Email.Should().Be("nhanvien@gmail.com");
        info.FullName.Should().Be("Hà Đông Hải");
    }
}
