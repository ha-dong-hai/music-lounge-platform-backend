using Xunit.Abstractions;

namespace MusicLounge.Tests.Integration.DemoData;

/// <summary>
/// MLACP-577. Hai điểm vào chạy tay cho bộ DỮ LIỆU MẪU đa dạng (<see cref="SampleDataBuilder"/>). Cùng cách bảo vệ với
/// <see cref="DemoDataScript"/>: phải có mặt cả hai biến môi trường, không biến nào có giá trị mặc định, và luôn có sẵn
/// đường xoá sạch. Câu xác nhận KHÁC với bộ MLACP-324 để không ai chạy nhầm bộ này khi định chạy bộ kia.
///
/// <code>
/// $env:SAMPLE_SEED_CONNECTION = "&lt;chuỗi kết nối&gt;"
/// $env:SAMPLE_SEED_CONFIRM    = "yes-seed-sample-data"
/// dotnet test --filter "FullyQualifiedName~SampleDataScript.Seed"    # sinh
/// dotnet test --filter "FullyQualifiedName~SampleDataScript.Clean"   # xoá sạch
/// </code>
///
/// Không đặt biến thì cả hai hàm không làm gì và vẫn xanh — chạy cả bộ test hay chạy trên CI đều không đụng tới database
/// nào. Có <c>scripts/demo-data.ps1 -Sample</c> gói lại cho gọn.
///
/// THỨ TỰ BẮT BUỘC khi chạy lên môi trường thật: diễn tập trên một bản sao SQL Server trước (bài kiểm tra chạy trên
/// SQLite, không kiểm khoá ngoại như SQL Server), ghi lại số dòng sẽ thêm vào deploy_log, rồi mới chạy thật.
/// </summary>
public sealed class SampleDataScript
{
    private const string ConnectionVariable = "SAMPLE_SEED_CONNECTION";
    private const string ConfirmVariable = "SAMPLE_SEED_CONFIRM";
    private const string ConfirmPhrase = "yes-seed-sample-data";

    private readonly ITestOutputHelper _output;

    public SampleDataScript(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Seed()
    {
        if (!Armed(out var connection)) return;

        await using var host = new SampleDataHost(connection);
        var ok = await new SampleDataBuilder(host, Log).SeedAsync();
        // Lượt dựng không thành (đã có dữ liệu mẫu, thiếu dữ liệu nền) phải ĐỎ: xanh ở đây là báo "đã dựng" trong khi
        // không có dòng nào được thêm.
        Assert.True(ok, "Không dựng được dữ liệu mẫu — xem các dòng nhật ký ở trên.");
    }

    [Fact]
    public async Task Clean()
    {
        if (!Armed(out var connection)) return;

        await using var host = new SampleDataHost(connection);
        await new SampleDataBuilder(host, Log).CleanAsync();
    }

    // Ghi ra cả console: `dotnet test` chỉ in ITestOutputHelper khi bật verbosity chi tiết, mà người chạy script cần thấy
    // con số ngay.
    private void Log(string s)
    {
        _output.WriteLine(s);
        Console.WriteLine("[du-lieu-mau] " + s);
    }

    private bool Armed(out string connection)
    {
        connection = Environment.GetEnvironmentVariable(ConnectionVariable) ?? "";
        var confirm = Environment.GetEnvironmentVariable(ConfirmVariable) ?? "";

        if (connection.Length > 0 && confirm == ConfirmPhrase) return true;

        _output.WriteLine($"Bỏ qua: cần {ConnectionVariable}=<chuỗi kết nối> và {ConfirmVariable}={ConfirmPhrase}.");
        return false;
    }
}
