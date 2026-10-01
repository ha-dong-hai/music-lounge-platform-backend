using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.Livestreams;

/// <summary>
/// MLACP-512 (fe M-441/M-442, đo bằng Mux thật 01/10). Mux trả 400 khi XOÁ một luồng còn 'active' (OBS vẫn phát) — nên
/// bấm Kết thúc lúc encoder còn đẩy tín hiệu thì luồng không bị xoá, tiếp tục chạy và bị tính tiền. Phải TẮT rồi mới xoá.
/// Mux giả dưới đây mô phỏng đúng hành vi đã đo: DELETE luồng active → 400; PUT disable → 200 rồi DELETE → 204.
/// </summary>
public sealed class MuxStreamDeleteTests
{
    private sealed class MuxGia(bool luongTonTai = true) : HttpMessageHandler
    {
        private bool _active = luongTonTai;
        private bool _tonTai = luongTonTai;
        public List<string> YeuCau { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            YeuCau.Add($"{req.Method} {req.RequestUri!.AbsolutePath}");
            HttpStatusCode code;
            if (!_tonTai) code = HttpStatusCode.NotFound;
            else if (req.Method == HttpMethod.Put && req.RequestUri.AbsolutePath.EndsWith("/disable")) { _active = false; code = HttpStatusCode.OK; }
            else if (req.Method == HttpMethod.Delete && _active) code = HttpStatusCode.BadRequest;
            else if (req.Method == HttpMethod.Delete) { _tonTai = false; code = HttpStatusCode.NoContent; }
            else code = HttpStatusCode.MethodNotAllowed;
            return Task.FromResult(new HttpResponseMessage(code));
        }
    }

    private sealed class MotClient(HttpMessageHandler h) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(h, disposeHandler: false);
    }

    private static MuxStreamService Mux(MuxGia gia)
        => new(new MotClient(gia), Options.Create(new MuxSettings { TokenId = "id", TokenSecret = "secret" }));

    [Fact]
    public async Task LuongDangActive_TatRoiMoiXoa_KhongBiMux400()
    {
        var gia = new MuxGia();

        await Mux(gia).DeleteStreamAsync("ls123");

        gia.YeuCau.Should().Equal("PUT /video/v1/live-streams/ls123/disable", "DELETE /video/v1/live-streams/ls123");
    }

    [Fact]
    public async Task LuongDaKhongConTrenMux_KhongBaoLoi()
    {
        var gia = new MuxGia(luongTonTai: false);

        var act = () => Mux(gia).DeleteStreamAsync("da-xoa");

        await act.Should().NotThrowAsync("luồng không còn thì không còn gì bị tính tiền");
        gia.YeuCau.Should().Equal("PUT /video/v1/live-streams/da-xoa/disable");
    }

    [Fact]
    public async Task MuxLoiThat_VanBaoRaNgoai()
    {
        var gia = new LoiServer();

        var act = () => new MuxStreamService(new MotClient(gia),
            Options.Create(new MuxSettings { TokenId = "id", TokenSecret = "secret" })).DeleteStreamAsync("x");

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    private sealed class LoiServer : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}
