using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Tests.Integration.Observability;

/// <summary>
/// MLACP-415. Azure SQL reset kết nối vài lần mỗi ngày ("an error occurred during the login process ... Connection reset
/// by peer" trong log 14–15/09). Không bật retry thì mỗi lần như vậy là một lỗi 500 giữa chừng một lệnh. Bật retry lại
/// đòi hỏi transaction phải nằm trong execution strategy, nếu không EF ném lỗi cấu hình ngay khi chạy.
/// </summary>
public sealed class SqlRetryTests
{
    [Fact]
    public void CauHinhSqlServer_BatTuThuLaiKhiKetNoiRot()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=tcp:vi-du.database.windows.net;Database=x;User Id=u;Password=p;",
                ["Jwt:Secret"] = "MusicLounge-Testing-Secret-Key-MinLength32Chars!",
            }).Build());
        services.AddLogging();
        services.AddInfrastructure(services.BuildServiceProvider().GetRequiredService<IConfiguration>());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        var sqlServer = options.FindExtension<SqlServerOptionsExtension>();

        sqlServer.Should().NotBeNull("ứng dụng thật chạy trên SQL Server");
        sqlServer!.ExecutionStrategyFactory.Should().NotBeNull(
            "thiếu EnableRetryOnFailure thì mỗi lần Azure SQL reset kết nối là một lỗi 500 cho người dùng");
    }

    [Fact]
    public async Task DonViCongViec_ChayLaiThaoTacKhiExecutionStrategyYeuCau()
    {
        // Dùng một execution strategy tự viết: lần đầu ném lỗi "thoáng qua", lần hai chạy bình thường — đúng hình dạng
        // của một lần Azure SQL reset kết nối. Nhà cung cấp SQLite trong test không có retry sẵn.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:", o => o.ExecutionStrategy(d => new ThuLaiMotLan(d)))
            .Options;
        await using var ctx = new ApplicationDbContext(options);
        IUnitOfWork uow = new UnitOfWork(ctx);
        var soLanChay = 0;

        var ketQua = await uow.ExecuteWithRetryAsync(_ =>
        {
            soLanChay++;
            if (soLanChay == 1) throw new TimeoutException("ket noi bi reset");
            return Task.FromResult("xong");
        });

        ketQua.Should().Be("xong");
        soLanChay.Should().Be(2, "thao tác phải được chạy lại nguyên vẹn sau lỗi thoáng qua");
    }

    private sealed class ThuLaiMotLan(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, maxRetryCount: 1, maxRetryDelay: TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TimeoutException;
    }
}
