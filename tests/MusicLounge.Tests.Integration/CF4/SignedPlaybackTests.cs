using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// MLACP-647. Mọi luồng Mux từng được tạo "public": người có vé chép https://stream.mux.com/{id}.m3u8 từ công cụ trình
/// duyệt là ai cũng xem được, giới hạn 2 thiết bị / vé chỉ chặn trên web (đọc mã 05/10/2026). Nay buổi có phí dùng
/// playback policy "signed" khi đã cấu hình khoá ký, và mỗi người có quyền nhận link kèm token RS256 có hạn.
/// <para>Không gọi Mux thật (tốn phí, cần khoá). Token được kiểm bằng khoá công khai của chính cặp khoá sinh trong test,
/// đúng cách Mux kiểm.</para>
/// </summary>
[Collection("Integration")]
public sealed class SignedPlaybackTests
{
    private readonly ApiFactory _factory;

    public SignedPlaybackTests(ApiFactory factory) => _factory = factory;

    private sealed class KhongDung : IHttpClientFactory { public HttpClient CreateClient(string name) => throw new InvalidOperationException(); }

    private static (MuxStreamService Service, RSA Key) MuxCoKhoa(bool coKhoa = true)
    {
        var rsa = RSA.Create(2048);
        var pem = Convert.ToBase64String(Encoding.UTF8.GetBytes(rsa.ExportRSAPrivateKeyPem()));
        var settings = coKhoa ? new MuxSettings { SigningKeyId = "kid-647", SigningKeyPrivate = pem } : new MuxSettings();
        return (new MuxStreamService(new KhongDung(), Options.Create(settings)), rsa);
    }

    private static JsonElement Phan(string jwt, int i)
    {
        var s = jwt.Split('.')[i].Replace('-', '+').Replace('_', '/');
        return JsonDocument.Parse(Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='))).RootElement;
    }

    [Fact]
    public void ASignedStream_GetsAViewerTokenThatMuxCanVerify()
    {
        var (mux, rsa) = MuxCoKhoa();
        var het = DateTimeOffset.UtcNow.AddHours(3);

        var url = mux.ViewerPlaybackUrl("https://stream.mux.com/AbC123.m3u8" + MuxStreamService.SignedMarker, het);

        url.Should().StartWith("https://stream.mux.com/AbC123.m3u8?token=");
        var jwt = url[(url.IndexOf("token=", StringComparison.Ordinal) + 6)..];
        var parts = jwt.Split('.');
        var chuKy = Convert.FromBase64String(parts[2].Replace('-', '+').Replace('_', '/').PadRight(parts[2].Length + (4 - parts[2].Length % 4) % 4, '='));
        rsa.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), chuKy, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeTrue("Mux verifies the RS256 signature with the signing key's public half");
        Phan(jwt, 0).GetProperty("kid").GetString().Should().Be("kid-647");
        Phan(jwt, 0).GetProperty("alg").GetString().Should().Be("RS256");
        Phan(jwt, 1).GetProperty("sub").GetString().Should().Be("AbC123");
        Phan(jwt, 1).GetProperty("aud").GetString().Should().Be("v");
        Phan(jwt, 1).GetProperty("exp").GetInt64().Should().Be(het.ToUnixTimeSeconds(), "a copied link must stop working");
    }

    [Fact]
    public void APublicStream_IsReturnedUnchanged()
        => MuxCoKhoa().Service.ViewerPlaybackUrl("https://stream.mux.com/Pub1.m3u8", DateTimeOffset.UtcNow.AddHours(1))
            .Should().Be("https://stream.mux.com/Pub1.m3u8");

    [Fact]
    public void WithoutSigningKeys_TheMarkerIsDropped_AndNothingIsSigned()
        => MuxCoKhoa(coKhoa: false).Service.ViewerPlaybackUrl("https://stream.mux.com/S1.m3u8" + MuxStreamService.SignedMarker, DateTimeOffset.UtcNow.AddHours(1))
            .Should().Be("https://stream.mux.com/S1.m3u8");

    [Fact]
    public async Task TheDetailEndpoint_HandsTheTicketHolderAPlaybackUrlFromTheProvider_AndNoOneElse()
    {
        // Buổi mẫu (SeedHelper.ShowId) có vé xem trực tuyến của khán giả mẫu; gắn một luồng có phí đánh dấu "signed".
        Guid lsId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId, Name = $"Signed647-{Guid.NewGuid():N}"[..24], Format = LoungeShowFormat.Online,
                Status = LoungeShowStatus.Ongoing, ScheduledStart = DateTimeOffset.UtcNow.AddMinutes(-10),
                ScheduledEnd = DateTimeOffset.UtcNow.AddHours(2)
            };
            db.Add(show);
            await db.SaveChangesAsync();
            var tier = new TicketTier { LoungeShowId = show.Id, Name = "Online", AccessType = AccessType.Livestream, TotalCapacity = 5 };
            db.Add(tier);
            await db.SaveChangesAsync();
            var price = new TicketPrice { TierId = tier.Id, Name = "Gia", Price = 100_000m, Quota = 5, IsActive = true, SaleStart = DateTimeOffset.UtcNow.AddDays(-1), PurchaseChannel = PurchaseChannel.Online };
            db.Add(price);
            await db.SaveChangesAsync();
            db.Add(new Ticket { Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = price.Id, TierId = tier.Id, ShowId = show.Id, Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow });
            var ls = new Livestream
            {
                LoungeShowId = show.Id, Provider = "fake", Status = LivestreamStatus.Live, IsFree = false,
                HlsUrl = "https://fake.hls.test/p647.m3u8" + MuxStreamService.SignedMarker
            };
            db.Add(ls);
            await db.SaveChangesAsync();
            lsId = ls.Id;
        }

        var holder = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
            .GetFromJsonAsync<JsonElement>($"/api/v1/livestreams/{lsId}");
        holder.GetProperty("data").GetProperty("hlsUrl").GetString().Should().Be("https://fake.hls.test/p647.m3u8?token=fake",
            "the stored URL is never handed out as-is — the provider turns it into a viewer link");

        var stranger = await _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner")
            .GetFromJsonAsync<JsonElement>($"/api/v1/livestreams/{lsId}");
        stranger.GetProperty("data").GetProperty("hlsUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
