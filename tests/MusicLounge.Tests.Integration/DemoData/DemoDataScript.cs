using Microsoft.EntityFrameworkCore;
using MusicLounge.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace MusicLounge.Tests.Integration.DemoData;

/// <summary>
/// MLACP-324. Hai điểm vào chạy tay cho bộ dữ liệu trình diễn — phần việc nằm ở
/// <see cref="DemoDataBuilder"/>, ở đây chỉ có phần quyết định CÓ ĐƯỢC CHẠY HAY KHÔNG.
///
/// <b>Vì sao cần bộ dữ liệu này.</b> Đo trên production ngày 2026-09-09: 7 tương tác, 0 người khai
/// sở thích, 0 người bật đồng ý AI, 2 người có từ 2 tương tác trở lên (lọc cộng tác cần 10). Mọi
/// nhánh của hệ gợi ý đều đói dữ liệu, nên lúc trình diễn nó sẽ trông như AI không làm gì. Đó là
/// vấn đề của dữ liệu, không phải của code — viết thêm code không sửa được.
///
/// <b>Vì sao là script chứ không phải endpoint API.</b> Một endpoint sinh dữ liệu giả mà lỡ được
/// gọi trên môi trường thật thì không gỡ lại được. Ở đây phải có mặt cả hai biến môi trường, không
/// biến nào có giá trị mặc định, và luôn có sẵn đường xoá sạch.
///
/// <b>Vì sao nằm trong dự án test.</b> Vì nó dựng dữ liệu qua chính EF model của hệ thống, nên mọi
/// sai lệch lược đồ trở thành lỗi biên dịch chứ không thành dữ liệu hỏng âm thầm. Một file SQL thô
/// không có bảo đảm đó, và còn dính bẫy cột enum lưu dạng chuỗi — nơi một số nguyên được SQL Server
/// nhận mà không báo lỗi, rồi mọi truy vấn sau đó lặng lẽ không khớp dòng nào.
///
/// <code>
/// $env:DEMO_SEED_CONNECTION = "&lt;chuỗi kết nối&gt;"
/// $env:DEMO_SEED_CONFIRM    = "yes-seed-demo-data"
/// dotnet test --filter "FullyQualifiedName~DemoDataScript.Seed"    # sinh
/// dotnet test --filter "FullyQualifiedName~DemoDataScript.Clean"   # xoá sạch
/// </code>
///
/// Không đặt biến thì cả hai hàm không làm gì và vẫn xanh, nên chạy cả bộ test như thường lệ hoặc
/// chạy trên CI đều không đụng tới database nào. Có <c>scripts/demo-data.ps1</c> gói lại cho gọn.
/// </summary>
public sealed class DemoDataScript
{
    private const string ConnectionVariable = "DEMO_SEED_CONNECTION";
    private const string ConfirmVariable = "DEMO_SEED_CONFIRM";
    private const string ConfirmPhrase = "yes-seed-demo-data";

    private readonly ITestOutputHelper _output;

    public DemoDataScript(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Seed()
    {
        if (!Armed(out var connection)) return;

        await using var db = Open(connection);
        await new DemoDataBuilder(db, _output.WriteLine).SeedAsync();
    }

    [Fact]
    public async Task Clean()
    {
        if (!Armed(out var connection)) return;

        await using var db = Open(connection);
        await new DemoDataBuilder(db, _output.WriteLine).CleanAsync();
    }

    /// <summary>
    /// Hai chìa khoá, không chìa nào có mặc định: một chuỗi kết nối, và một câu xác nhận phải gõ
    /// đúng từng chữ. Thiếu một trong hai thì không làm gì và nói rõ vì sao.
    /// </summary>
    private bool Armed(out string connection)
    {
        connection = Environment.GetEnvironmentVariable(ConnectionVariable) ?? "";
        var confirm = Environment.GetEnvironmentVariable(ConfirmVariable) ?? "";

        if (connection.Length > 0 && confirm == ConfirmPhrase) return true;

        _output.WriteLine(
            $"Bỏ qua: cần {ConnectionVariable}=<chuỗi kết nối> và {ConfirmVariable}={ConfirmPhrase}.");
        return false;
    }

    private static ApplicationDbContext Open(string connection)
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection)
            .Options);
}
