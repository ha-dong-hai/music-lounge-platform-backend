using System.Diagnostics;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;
using Xunit.Abstractions;

namespace MusicLounge.Tests.Integration.Benchmarks;

/// <summary>
/// MLACP-312. Đo thời gian phản hồi cho yêu cầu phi chức năng "dưới 3 giây".
///
/// <b>Bộ test này đo cái gì.</b> Thời gian từ lúc request vào pipeline ASP.NET Core đến lúc response
/// ra: định tuyến, xác thực, phân quyền, MediatR pipeline, handler, truy vấn database, dựng DTO,
/// tuần tự hoá JSON. Đó là toàn bộ phần thời gian do mã nguồn này quyết định.
///
/// <b>Và nó KHÔNG đo cái gì.</b> Không có độ trễ mạng, không có TLS handshake, không có thời gian
/// khởi động lạnh của App Service, và database là SQLite trong bộ nhớ chứ không phải Azure SQL. Con
/// số ở đây vì vậy là <b>cận dưới</b>, không phải câu trả lời cho NFR. Nói "đã đạt dưới 3 giây" chỉ
/// dựa trên bộ test này là nói quá.
///
/// Đó cũng là lý do harness nhận biến môi trường <c>PERF_BASE_URL</c>: trỏ vào môi trường đã
/// triển khai thật thì cùng phép đo này cho ra con số dùng được cho NFR, gồm cả mạng và Azure SQL.
/// Không đặt biến đó thì nó chạy trong tiến trình và chỉ đo phần mã nguồn.
///
/// <b>Vì sao ngưỡng khẳng định đặt rất rộng.</b> Máy chạy CI có tải không đoán trước được, nên một
/// ngưỡng sát sẽ đỏ vì lý do không liên quan gì tới chất lượng mã. Ngưỡng ở đây đặt để bắt hồi quy
/// bệnh lý — kiểu truy vấn N+1 mới xuất hiện làm một endpoint chậm đi hàng chục lần — chứ không
/// phải để chứng minh NFR. Con số thật in ra ở output của test.
/// </summary>
[Collection("Integration")]
public sealed class ResponseTimeBudgetTests
{
    private readonly ApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public ResponseTimeBudgetTests(ApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private const int WarmupRequests = 3;
    private const int MeasuredRequests = 20;

    /// <summary>
    /// Ngưỡng bắt hồi quy bệnh lý, không phải ngưỡng NFR. Xem chú thích đầu lớp: phép đo này không
    /// bao gồm mạng và không chạy trên database thật, nên so nó với mốc 3 giây là so nhầm thứ.
    /// </summary>
    private const int RegressionCeilingMs = 2000;

    private static string? RemoteBaseUrl => Environment.GetEnvironmentVariable("PERF_BASE_URL");

    private HttpClient Client(bool asOwner)
    {
        if (RemoteBaseUrl is { Length: > 0 } url)
            return new HttpClient { BaseAddress = new Uri(url) };

        return asOwner
            ? _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId)
            : _factory.CreateClient();
    }

    private sealed record Measurement(string Endpoint, double P50, double P95, double Max, int Samples);

    private async Task<Measurement> MeasureAsync(string label, string path, bool asOwner = false)
    {
        var client = Client(asOwner);

        // Bỏ những lần gọi đầu: lần đầu chạm một endpoint còn phải biên dịch truy vấn EF, dựng
        // cây biểu thức, nạp assembly. Đo lẫn phần đó vào là đo chi phí khởi động một lần, không
        // phải chi phí phục vụ một người dùng.
        for (var i = 0; i < WarmupRequests; i++)
            (await client.GetAsync(path)).EnsureSuccessStatusCode();

        var samples = new List<double>(MeasuredRequests);
        for (var i = 0; i < MeasuredRequests; i++)
        {
            var sw = Stopwatch.StartNew();
            var res = await client.GetAsync(path);
            sw.Stop();
            res.EnsureSuccessStatusCode();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        var p50 = samples[samples.Count / 2];
        var p95 = samples[(int)Math.Min(samples.Count - 1, Math.Ceiling(samples.Count * 0.95) - 1)];

        return new Measurement(label, p50, p95, samples[^1], samples.Count);
    }

    private void Report(params Measurement[] measurements)
    {
        _output.WriteLine(RemoteBaseUrl is { Length: > 0 } url
            ? $"Đo trên môi trường đã triển khai: {url} (gồm cả mạng và database thật)"
            : "Đo trong tiến trình, database SQLite trong bộ nhớ — KHÔNG gồm mạng và Azure SQL. " +
              "Đây là cận dưới, không phải câu trả lời cho NFR 3 giây.");
        _output.WriteLine($"{MeasuredRequests} lần đo mỗi endpoint, sau {WarmupRequests} lần làm nóng.");
        _output.WriteLine("");
        _output.WriteLine($"{"Endpoint",-42}{"p50 (ms)",12}{"p95 (ms)",12}{"max (ms)",12}");

        foreach (var m in measurements)
            _output.WriteLine($"{m.Endpoint,-42}{m.P50,12:F1}{m.P95,12:F1}{m.Max,12:F1}");
    }

    [Fact]
    public async Task ThePagesAnAudienceWaitsOn_RespondWellInsideTheBudget()
    {
        // Bốn màn hình đầu tiên một khán giả chạm vào. Nếu chỗ nào chậm thì phải là chỗ này, vì
        // đây là nơi có nhiều người dùng đồng thời nhất và là ấn tượng đầu tiên về nền tảng.
        var results = new[]
        {
            await MeasureAsync("GET /lounge-shows", "/api/v1/lounge-shows?pageSize=20"),
            await MeasureAsync("GET /lounge-shows/search", "/api/v1/lounge-shows/search?pageSize=20"),
            await MeasureAsync("GET /lounges", "/api/v1/lounges?pageSize=20"),
            await MeasureAsync("GET /lounges/{id}", $"/api/v1/lounges/{SeedHelper.LoungeId}")
        };

        Report(results);

        foreach (var m in results)
            m.P95.Should().BeLessThan(RegressionCeilingMs,
                $"{m.Endpoint} vượt ngưỡng bắt hồi quy — gần như chắc chắn là một truy vấn mới " +
                "chạy lặp theo từng dòng, không phải máy chạy chậm");
    }

    [Fact]
    public async Task ThePagesABuyerWaitsOnBeforePaying_RespondWellInsideTheBudget()
    {
        // Đường đi tới lúc trả tiền. Chậm ở đây là chậm ở đúng chỗ tốn tiền nhất.
        var results = new[]
        {
            await MeasureAsync("GET /lounge-shows/{id}", $"/api/v1/lounge-shows/{SeedHelper.OfflineShowId}"),
            await MeasureAsync("GET /ticket-tiers", $"/api/v1/ticket-tiers?showId={SeedHelper.OfflineShowId}")
        };

        Report(results);

        foreach (var m in results)
            m.P95.Should().BeLessThan(RegressionCeilingMs, $"{m.Endpoint} vượt ngưỡng bắt hồi quy");
    }

    [Fact]
    public async Task TheOwnerDashboard_RespondsWellInsideTheBudget()
    {
        // Màn hình tổng hợp nặng nhất phía chủ phòng trà: nhiều phép gộp trên vé, doanh thu, đối
        // soát. Đây là endpoint dễ phình chi phí nhất khi dữ liệu lớn dần.
        var results = new[]
        {
            await MeasureAsync("GET /analytics/my-lounge",
                $"/api/v1/analytics/my-lounge?loungeId={SeedHelper.LoungeId}", asOwner: true)
        };

        Report(results);

        results[0].P95.Should().BeLessThan(RegressionCeilingMs);
    }
}
