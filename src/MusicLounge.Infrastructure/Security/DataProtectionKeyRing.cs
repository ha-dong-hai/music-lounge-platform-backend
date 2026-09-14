using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace MusicLounge.Infrastructure.Security;

/// <summary>
/// MLACP-400. Nơi lưu bộ khoá Data Protection, và việc giữ nó qua các lần triển khai.
///
/// <para>Bộ khoá giữ khả năng giải mã mọi cột PII (số tài khoản nhận tiền, số CCCD/CMND, mã số thuế) và tham số job Hangfire —
/// mất bộ khoá là mất các giá trị đó vĩnh viễn. Trước đây nó nằm trong <c>App_Data/dataprotection-keys</c> bên trong thư mục
/// site (<c>/home/site/wwwroot</c> trên Azure). Lần triển khai 04/09/2026 (zip, xoá sạch thư mục đích) xoá nó; app âm thầm
/// sinh khoá mới, và mọi giá trị mã hoá trước đó không còn đọc được (xác minh trên Azure ngày 14/09/2026).</para>
///
/// <para>Trên Azure App Service, Microsoft Learn ("Data Protection key management and lifetime") ghi khoá được lưu ở thư mục
/// ASP.NET/DataProtection-Keys dưới %HOME%, "backed by network storage and is synchronized across all machines hosting the
/// app". Thư mục đó nằm ngoài thư mục site nên triển khai không đụng tới. Cùng trang ghi "Keys aren't protected at rest" —
/// trên Linux mức bảo vệ này giữ nguyên như trước.</para>
/// </summary>
public static class DataProtectionKeyRing
{
    public static IDataProtectionBuilder AddMusicLoungeDataProtection(
        this IServiceCollection services, string contentRoot, Func<string, string?> getEnvironmentVariable)
    {
        var keyDirectory = ResolveDirectory(contentRoot, getEnvironmentVariable);
        var copied = CopyMissingLegacyKeys(LegacyDirectory(contentRoot), keyDirectory);
        if (copied > 0)
            Console.WriteLine($"Data Protection: đã chép {copied} khoá cũ từ App_Data sang {keyDirectory}.");

        var builder = services.AddDataProtection()
            .SetApplicationName("MusicLounge")
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
        // Máy chạy Windows: khoá được mã hoá lúc lưu bằng DPAPI cấp máy. Linux (Azure App Service) không có DPAPI.
        if (OperatingSystem.IsWindows())
            builder.ProtectKeysWithDpapi(protectToLocalMachine: true);
        return builder;
    }

    /// <summary>
    /// Azure App Service được nhận ra qua <c>WEBSITE_SITE_NAME</c>. Chỉ có <c>HOME</c> thì không đủ: mọi máy Linux, kể cả
    /// runner CI, đều có <c>HOME</c>.
    /// </summary>
    public static string ResolveDirectory(string contentRoot, Func<string, string?> getEnvironmentVariable)
    {
        var siteName = getEnvironmentVariable("WEBSITE_SITE_NAME");
        var home = getEnvironmentVariable("HOME");
        return !string.IsNullOrWhiteSpace(siteName) && !string.IsNullOrWhiteSpace(home)
            ? Path.Combine(home, "ASP.NET", "DataProtection-Keys")
            : LegacyDirectory(contentRoot);
    }

    public static string LegacyDirectory(string contentRoot) => Path.Combine(contentRoot, "App_Data", "dataprotection-keys");

    /// <summary>
    /// Chép các file khoá có ở thư mục cũ mà thư mục mới chưa có. Không bao giờ ghi đè: khoá cùng tên ở thư mục mới là khoá
    /// app đang dùng. Chạy mỗi lần khởi động và không làm gì nếu không còn gì để chép.
    /// </summary>
    public static int CopyMissingLegacyKeys(string legacyDirectory, string targetDirectory)
    {
        if (!Directory.Exists(legacyDirectory)) return 0;

        Directory.CreateDirectory(targetDirectory);
        var copied = 0;
        foreach (var source in Directory.GetFiles(legacyDirectory, "key-*.xml"))
        {
            var destination = Path.Combine(targetDirectory, Path.GetFileName(source));
            if (File.Exists(destination)) continue;
            File.Copy(source, destination, overwrite: false);
            copied++;
        }
        return copied;
    }
}
