using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Fakes;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.DemoData;

/// <summary>
/// MLACP-577. Thứ <see cref="SampleDataBuilder"/> cần ở một máy chủ: dịch vụ của ứng dụng, và một HttpClient đã "đăng
/// nhập" thành một người dùng. Có hai bản: <see cref="ApiFactory"/> (SQLite, để bài kiểm tra chạy đúng đoạn mã dựng dữ
/// liệu) và <see cref="SampleDataHost"/> (SQL Server, để dựng thật).
/// </summary>
internal interface ISampleHost
{
    IServiceProvider Services { get; }
    HttpClient ClientFor(Guid userId, string role);
}

internal sealed class ApiFactorySampleHost : ISampleHost
{
    private readonly ApiFactory _factory;
    public ApiFactorySampleHost(ApiFactory factory) => _factory = factory;
    public IServiceProvider Services => _factory.Services;
    public HttpClient ClientFor(Guid userId, string role) => _factory.CreateAuthenticatedClient(userId, role);
}

/// <summary>
/// MLACP-577. CHÍNH ứng dụng MusicLounge, chạy trong tiến trình, trỏ vào database đích (SQL Server).
///
/// <b>Vì sao phải dựng cả ứng dụng thay vì ghi thẳng bảng.</b> Chủ dự án yêu cầu vé dựng ra phải kèm thanh toán và sổ cái
/// khớp nhau. Số tiền của một vé (hoa hồng, thuế GTGT, thuế TNCN, phần phòng trà nhận), bút toán sổ kép và lịch quyết
/// toán đều do mã của hệ thống tính lúc xác nhận thanh toán (ProcessVnPayCallback → WriteTicketLedgerHandler,
/// ScheduleSettlementHandler). Chép lại các công thức đó vào một script là có HAI bản của cùng một luật tiền — bản script
/// sẽ lệch ngay lần đổi tỉ lệ đầu tiên. Nên bộ dựng mua vé qua đúng API (giữ chỗ → thanh toán → callback), và hệ thống
/// tự ghi phần còn lại.
///
/// <b>Khác với máy chủ thật ở đúng những chỗ phải khác:</b>
/// - VNPay là bản giả: không có tiền thật nào đi qua cổng, và không gọi ra ngoài. Hệ quả cần biết: các thanh toán này
///   mang mã giao dịch không tồn tại ở VNPay — yêu cầu hoàn tiền cho vé mẫu sẽ không hoàn được qua cổng.
/// - Hangfire dùng bộ nhớ trong và KHÔNG có server: không job nền nào của tiến trình này chạy trên database đích (job
///   định kỳ của máy chủ thật vẫn chạy như thường). Việc được đưa vào hàng đợi trong lúc dựng (ghi nhật ký hành vi,
///   thông báo đẩy, email) vì thế KHÔNG chạy — bộ dựng tự ghi nhật ký hành vi, và không ai bị gửi email/thông báo.
/// - Đăng nhập bằng header (TestAuthHandler) — chỉ tồn tại trong tiến trình này, không mở cổng mạng nào.
/// </summary>
internal sealed class SampleDataHost : WebApplicationFactory<Program>, ISampleHost
{
    private readonly string _connection;

    public SampleDataHost(string connection) => _connection = connection;

    public HttpClient ClientFor(Guid userId, string role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderUserId, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRole, role);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseSqlServer(_connection, sql => sql.EnableRetryOnFailure()));

            // Cùng cách gỡ Hangfire như ApiFactory (xem chú thích MLACP-435 ở đó): gỡ cả hosted service đăng ký bằng
            // factory, nếu không BackgroundJobServer vẫn chạy và job nền sẽ chạy trên database đích.
            var hangfire = services
                .Where(d =>
                    d.ServiceType.Namespace?.StartsWith("Hangfire") == true ||
                    d.ImplementationType?.Namespace?.StartsWith("Hangfire") == true ||
                    d.ImplementationFactory?.GetType().GenericTypeArguments.LastOrDefault()?.Namespace
                        ?.StartsWith("Hangfire") == true)
                .ToList();
            foreach (var d in hangfire) services.Remove(d);
            services.AddHangfire(cfg => cfg.UseInMemoryStorage(new InMemoryStorageOptions()));

            services.RemoveAll<IVnPayService>();
            services.AddSingleton<IVnPayService, FakeVnPayService>();

            services.RemoveAll<ILivestreamServiceFactory>();
            services.AddSingleton<ILivestreamServiceFactory, FakeLivestreamServiceFactory>();
            foreach (var d in services.Where(d => d.ServiceType == typeof(ILivestreamService)).ToList())
                services.Remove(d);

            services.RemoveAll<IAIRecommendationService>();
            services.AddSingleton<IAIRecommendationService, FakeAiService>();

            services.RemoveAll<IFcmService>();
            services.AddSingleton<IFcmService, FakeFcmService>();

            services.RemoveAll<IGoogleTokenVerifier>();
            services.AddSingleton<IGoogleTokenVerifier, FakeGoogleTokenVerifier>();

            services.RemoveAll<IPanoramaStitchingService>();
            services.AddSingleton<IPanoramaStitchingService, FakePanoramaStitchingService>();

            services.RemoveAll<ILivestreamHubService>();
            services.AddSingleton<ILivestreamHubService, RecordingLivestreamHubService>();

            services.AddAuthentication(opts =>
            {
                opts.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                opts.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
