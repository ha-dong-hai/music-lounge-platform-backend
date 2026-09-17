using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.InMemory;
using Hangfire.Storage;
using MusicLounge.Infrastructure.Jobs;

namespace MusicLounge.Tests.Integration.Helpers;

/// <summary>
/// MLACP-440. Hangfire không tự xoá job định kỳ mà code đã thôi đăng ký. Azure 17/09 còn 4 job như vậy, đều lỗi
/// "Could not load type" vì lớp job không tồn tại trong code này.
///
/// Dùng InMemoryStorage riêng cho từng test, không dùng JobStorage.Current dùng chung (xem
/// feedback Hangfire test: client tĩnh bám vào storage đầu tiên).
/// </summary>
public sealed class OrphanRecurringJobCleanupTests
{
    private static Job JobThat() => Job.FromExpression<ReleaseExpiredHoldsJob>(j => j.ExecuteAsync(JobCancellationToken.Null));

    /// <summary>
    /// Tạo job định kỳ trỏ tới một lớp KHÔNG tồn tại — đúng tình trạng trên Azure. API công khai không cho đăng ký kiểu
    /// không nạp được, nên đăng ký bằng lớp thật rồi sửa tên lớp trong bản ghi đã tuần tự hoá.
    /// </summary>
    private static void TaoJobMoCoi(InMemoryStorage storage, string id)
    {
        new RecurringJobManager(storage).AddOrUpdate(id, JobThat(), Cron.Hourly());
        using var connection = storage.GetConnection();
        var hash = connection.GetAllEntriesFromHash($"recurring-job:{id}")!;
        hash["Job"] = hash["Job"].Replace(nameof(ReleaseExpiredHoldsJob), "VnPayReconciliationJob");
        using var tx = connection.CreateWriteTransaction();
        tx.SetRangeInHash($"recurring-job:{id}", hash);
        tx.Commit();
    }

    [Fact]
    public void GoJobDinhKyMoCoiKhongNapDuocLop_GiuJobDangDangKy()
    {
        var storage = new InMemoryStorage();
        new RecurringJobManager(storage).AddOrUpdate("release-expired-holds", JobThat(), Cron.Minutely());
        TaoJobMoCoi(storage, "reconcile-vnpay-payments");

        using (var connection = storage.GetConnection())
        {
            // Mô phỏng phải đúng hiện trạng Azure: Hangfire không nạp được lớp của job mồ côi.
            connection.GetRecurringJobs().Single(j => j.Id == "reconcile-vnpay-payments")
                .LoadException.Should().NotBeNull();
        }

        var removed = MusicLounge.Infrastructure.DependencyInjection.RemoveUnregisteredRecurringJobs(
            storage, ["release-expired-holds"]);

        removed.Should().Equal("reconcile-vnpay-payments");
        using var after = storage.GetConnection();
        after.GetRecurringJobs().Select(j => j.Id).Should().Equal("release-expired-holds");
    }

    [Fact]
    public void GoCaJobNapDuocLopNhungCodeKhongConDangKy()
    {
        // Đổi tên id trong code (job cũ vẫn chạy được) cũng để lại bản ghi thừa chạy song song với id mới.
        var storage = new InMemoryStorage();
        var manager = new RecurringJobManager(storage);
        manager.AddOrUpdate("ten-moi", JobThat(), Cron.Minutely());
        manager.AddOrUpdate("ten-cu", JobThat(), Cron.Minutely());

        var removed = MusicLounge.Infrastructure.DependencyInjection.RemoveUnregisteredRecurringJobs(storage, ["ten-moi"]);

        removed.Should().Equal("ten-cu");
    }

    [Fact]
    public void KhongCoJobThua_KhongGoGi()
    {
        var storage = new InMemoryStorage();
        new RecurringJobManager(storage).AddOrUpdate("release-expired-holds", JobThat(), Cron.Minutely());

        MusicLounge.Infrastructure.DependencyInjection.RemoveUnregisteredRecurringJobs(storage, ["release-expired-holds"])
            .Should().BeEmpty();
        using var connection = storage.GetConnection();
        connection.GetRecurringJobs().Should().ContainSingle();
    }
}

/// <summary>
/// MLACP-440. Bước dọn phải thật sự được gọi lúc app khởi động (ConfigureRecurringJobs) — các test ở trên chỉ gọi thẳng
/// hàm dọn nên sẽ vẫn xanh nếu ConfigureRecurringJobs quên gọi nó.
/// </summary>
[Collection("Integration")]
public sealed class ConfigureRecurringJobsRemovesOrphansTests
{
    public ConfigureRecurringJobsRemovesOrphansTests(ApiFactory factory) => _ = factory.Services; // host đã khởi động

    [Fact]
    public void ConfigureRecurringJobs_GoJobMoCoiTrongStorageDangDung()
    {
        var storage = JobStorage.Current;
        new RecurringJobManager(storage).AddOrUpdate("mo-coi-kiem-thu-440",
            Job.FromExpression<ReleaseExpiredHoldsJob>(j => j.ExecuteAsync(JobCancellationToken.Null)), Cron.Hourly());

        var removed = MusicLounge.Infrastructure.DependencyInjection.ConfigureRecurringJobs();

        removed.Should().Contain("mo-coi-kiem-thu-440");
        using var connection = storage.GetConnection();
        connection.GetRecurringJobs().Select(j => j.Id).Should().NotContain("mo-coi-kiem-thu-440")
            .And.Contain("release-expired-holds", "job code đang đăng ký phải còn nguyên");
    }
}
