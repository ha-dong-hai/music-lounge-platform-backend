using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MusicLounge.Infrastructure.Security;

namespace MusicLounge.Tests.Integration.Auth;

/// <summary>
/// MLACP-400. Bộ khoá Data Protection từng nằm trong thư mục site và bị lần triển khai 04/09/2026 xoá mất, kéo theo mọi giá trị
/// PII đã mã hoá. Trên Azure App Service nó phải nằm dưới HOME, ngoài thư mục site, và khoá cũ phải được mang sang — không có
/// bước mang sang thì đổi chỗ lưu cũng làm mất khả năng giải mã y như sự cố.
///
/// <para>Chỉ dùng thư mục tạm và biến môi trường giả — không đụng môi trường thật của máy chạy test.</para>
/// </summary>
public sealed class DataProtectionKeyRingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"mlacp400-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static Func<string, string?> Env(params (string Name, string? Value)[] variables)
        => name => variables.FirstOrDefault(v => v.Name == name).Value;

    private string Dir(params string[] parts) => Path.Combine(new[] { _root }.Concat(parts).ToArray());

    private static void WriteFile(string directory, string fileName, string content)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }

    // ── Chỗ lưu ──────────────────────────────────────────────────────────────

    [Fact]
    public void OnAzureAppService_KeysLiveUnderHome_OutsideTheSiteFolder()
    {
        var site = Dir("home", "site", "wwwroot");
        var home = Dir("home");

        var directory = DataProtectionKeyRing.ResolveDirectory(site, Env(("WEBSITE_SITE_NAME", "musiclounge-api"), ("HOME", home)));

        directory.Should().Be(Path.Combine(home, "ASP.NET", "DataProtection-Keys"));
        directory.Should().NotStartWith(site, "a deployment that wipes the site folder must not take the key ring with it");
    }

    [Fact]
    public void OutsideAzure_KeysStayInAppData()
    {
        DataProtectionKeyRing.ResolveDirectory(Dir("app"), Env())
            .Should().Be(Path.Combine(Dir("app"), "App_Data", "dataprotection-keys"));
    }

    [Fact]
    public void AMachineWithHomeButNoAppServiceSiteName_IsNotTreatedAsAzure()
    {
        // Mọi máy Linux, kể cả runner CI, đều có HOME.
        DataProtectionKeyRing.ResolveDirectory(Dir("app"), Env(("HOME", Dir("home"))))
            .Should().Be(Path.Combine(Dir("app"), "App_Data", "dataprotection-keys"));
    }

    // ── Mang khoá cũ sang ────────────────────────────────────────────────────

    [Fact]
    public void KeysMissingFromTheNewFolder_AreCopiedFromTheOldOne()
    {
        var legacy = Dir("site", "App_Data", "dataprotection-keys");
        var target = Dir("home", "ASP.NET", "DataProtection-Keys");
        WriteFile(legacy, "key-a.xml", "<key id='a'/>");
        WriteFile(legacy, "key-b.xml", "<key id='b'/>");

        DataProtectionKeyRing.CopyMissingLegacyKeys(legacy, target).Should().Be(2);

        File.ReadAllText(Path.Combine(target, "key-a.xml")).Should().Be("<key id='a'/>");
        File.ReadAllText(Path.Combine(target, "key-b.xml")).Should().Be("<key id='b'/>");
    }

    [Fact]
    public void AKeyAlreadyInTheNewFolder_IsNeverOverwritten()
    {
        var legacy = Dir("site", "App_Data", "dataprotection-keys");
        var target = Dir("home", "ASP.NET", "DataProtection-Keys");
        WriteFile(legacy, "key-a.xml", "OLD");
        WriteFile(legacy, "key-b.xml", "<key id='b'/>");
        WriteFile(target, "key-a.xml", "IN USE");

        DataProtectionKeyRing.CopyMissingLegacyKeys(legacy, target).Should().Be(1);

        File.ReadAllText(Path.Combine(target, "key-a.xml")).Should().Be("IN USE", "the key the app is using must not be replaced");
    }

    [Fact]
    public void OnlyKeyFilesAreCopied()
    {
        var legacy = Dir("site", "App_Data", "dataprotection-keys");
        var target = Dir("home", "ASP.NET", "DataProtection-Keys");
        WriteFile(legacy, "key-a.xml", "<key/>");
        WriteFile(legacy, "notes.txt", "not a key");

        DataProtectionKeyRing.CopyMissingLegacyKeys(legacy, target).Should().Be(1);

        File.Exists(Path.Combine(target, "notes.txt")).Should().BeFalse();
    }

    [Fact]
    public void NoOldFolder_CopiesNothing()
    {
        DataProtectionKeyRing.CopyMissingLegacyKeys(Dir("missing"), Dir("home", "ASP.NET", "DataProtection-Keys"))
            .Should().Be(0);
    }

    [Fact]
    public void SameFolder_CopiesNothing()
    {
        // Máy local: chỗ lưu mới trùng chỗ cũ.
        var folder = Dir("app", "App_Data", "dataprotection-keys");
        WriteFile(folder, "key-a.xml", "<key/>");

        DataProtectionKeyRing.CopyMissingLegacyKeys(folder, folder).Should().Be(0);
        File.ReadAllText(Path.Combine(folder, "key-a.xml")).Should().Be("<key/>");
    }

    // ── Đăng ký dịch vụ ──────────────────────────────────────────────────────

    [Fact]
    public void Registration_PointsTheKeyRingAtTheResolvedFolder_AndBringsTheKeyInUseAlong()
    {
        var site = Dir("home", "site", "wwwroot");
        var home = Dir("home");
        WriteFile(Path.Combine(site, "App_Data", "dataprotection-keys"), "key-in-use.xml", "<key/>");
        var services = new ServiceCollection().AddLogging();

        services.AddMusicLoungeDataProtection(site, Env(("WEBSITE_SITE_NAME", "musiclounge-api"), ("HOME", home)));

        using var provider = services.BuildServiceProvider();
        var expected = Path.Combine(home, "ASP.NET", "DataProtection-Keys");
        var repository = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;
        repository.Should().BeOfType<FileSystemXmlRepository>()
            .Which.Directory.FullName.TrimEnd(Path.DirectorySeparatorChar)
            .Should().Be(Path.GetFullPath(expected).TrimEnd(Path.DirectorySeparatorChar));
        File.Exists(Path.Combine(expected, "key-in-use.xml")).Should().BeTrue("the key the app is using today must come along");
    }
}

/// <summary>
/// MLACP-400. ASP.NET Core tự đăng ký Data Protection mặc định, nên bỏ lời gọi <c>AddMusicLoungeDataProtection</c> khỏi DI thì
/// mã hoá vẫn chạy và không bài nào khác đỏ — nhưng bộ khoá quay về chỗ lưu mặc định, đúng loại hỏng khiến dữ liệu mất khả năng
/// giải mã. Bài này chốt rằng app đang chạy thật dùng đúng bộ khoá do <see cref="DataProtectionKeyRing"/> chọn.
/// </summary>
[Collection("Integration")]
public sealed class DataProtectionKeyRingWiringTests
{
    private readonly ApiFactory _factory;

    public DataProtectionKeyRingWiringTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void TheRunningApp_KeepsItsKeyRingWhereDataProtectionKeyRingSays()
    {
        var expected = DataProtectionKeyRing.ResolveDirectory(Directory.GetCurrentDirectory(), Environment.GetEnvironmentVariable);

        var repository = _factory.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;

        repository.Should().BeOfType<FileSystemXmlRepository>()
            .Which.Directory.FullName.TrimEnd(Path.DirectorySeparatorChar)
            .Should().Be(Path.GetFullPath(expected).TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void TheRunningApp_ProtectsDataUnderTheMusicLoungeApplicationName()
    {
        // Đổi tên ứng dụng cũng làm khoá cũ không mở được dữ liệu cũ.
        _factory.Services.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator
            .Should().Be("MusicLounge");
    }
}
