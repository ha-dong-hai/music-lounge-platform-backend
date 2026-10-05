using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MusicLounge.Api.RateLimiting;

namespace MusicLounge.Tests.Integration.Security;

/// <summary>
/// MLACP-670. Hạn mức chung chia theo tài khoản khi đã đăng nhập, theo IP khi chưa. Trước đây chia theo IP, nên cả
/// một mạng dùng chung (WiFi trường lúc bảo vệ) chung 100 yêu cầu/phút.
///
/// <para>Bộ giới hạn tắt trong môi trường Testing (ApiFactory gửi hàng loạt yêu cầu từ một "IP"), nên test này kiểm
/// trực tiếp hàm chia khoá, và quét Program.cs để giữ thứ tự middleware mà hàm đó cần.</para>
/// </summary>
public sealed class RateLimitPartitionKeyTests
{
    private static HttpContext Ctx(string ip, ClaimsPrincipal? user = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (user is not null) ctx.User = user;
        return ctx;
    }

    private static ClaimsPrincipal SignedIn(Guid id)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "Bearer"));

    [Fact]
    public void TwoSignedInPeople_BehindTheSameIp_GetSeparateAllowances()
    {
        var a = RateLimitPartitionKey.For(Ctx("203.0.113.7", SignedIn(Guid.NewGuid())));
        var b = RateLimitPartitionKey.For(Ctx("203.0.113.7", SignedIn(Guid.NewGuid())));

        a.Should().NotBe(b, "cùng một WiFi không được làm người này hết hạn mức vì người kia");
        a.Should().StartWith("user:");
    }

    [Fact]
    public void AnonymousRequests_StillShareTheirIpsAllowance()
    {
        RateLimitPartitionKey.For(Ctx("203.0.113.7")).Should().Be("ip:203.0.113.7");
        RateLimitPartitionKey.For(Ctx("203.0.113.7")).Should().Be(RateLimitPartitionKey.For(Ctx("203.0.113.7")));
    }

    [Fact]
    public void AnUnauthenticatedIdentityWithAnIdClaim_IsNotTrusted()
    {
        // Claim không qua xác thực (IsAuthenticated = false) thì không được dùng để tách hạn mức — nếu không, ai cũng tự
        // đặt một mã ngẫu nhiên mỗi yêu cầu để thoát giới hạn.
        var forged = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())]));

        RateLimitPartitionKey.For(Ctx("203.0.113.7", forged)).Should().Be("ip:203.0.113.7");
    }

    [Fact]
    public void Program_AuthenticatesBeforeRateLimiting()
    {
        var program = FindProgramCs();
        var text = File.ReadAllText(program);
        var auth = text.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var limiter = text.IndexOf("app.UseRateLimiter();", StringComparison.Ordinal);

        auth.Should().BeGreaterThan(0, "quét phải tìm thấy UseAuthentication — không thấy thì test xanh vô nghĩa");
        limiter.Should().BeGreaterThan(0, "quét phải tìm thấy UseRateLimiter");
        auth.Should().BeLessThan(limiter,
            "bộ giới hạn chạy trước xác thực thì ctx.User luôn rỗng và mọi người lại chung hạn mức theo IP");
    }

    private static string FindProgramCs()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MusicLounge.sln"))) dir = dir.Parent;
        dir.Should().NotBeNull("phải tìm được thư mục gốc repo");
        return Path.Combine(dir!.FullName, "src", "MusicLounge.Api", "Program.cs");
    }
}
