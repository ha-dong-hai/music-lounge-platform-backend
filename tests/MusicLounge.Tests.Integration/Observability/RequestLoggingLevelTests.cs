using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;
using Serilog.Events;

namespace MusicLounge.Tests.Integration.Observability;

/// <summary>
/// MLACP-313. Nhật ký phải ghi đúng cái đã thật sự xảy ra.
///
/// Trước khi sửa, một request trả về 404 cho người gọi được ghi lại là
/// <c>[ERR] ... responded 500</c>. Nguyên nhân là thứ tự middleware: bộ ghi log nằm BÊN TRONG bộ
/// xử lý ngoại lệ, nên nó thấy ngoại lệ bay qua và kết luận 500, còn bộ xử lý ở ngoài mới viết lại
/// thành 404.
///
/// Người dùng nhận đúng mã — cái sai nằm hoàn toàn ở phần không ai nhìn. Và đó là lý do hàng trăm
/// test hiện có vẫn xanh: chúng khẳng định trên response, còn nhật ký thì không test nào đọc tới.
///
/// Hậu quả không nhỏ như vẻ ngoài: nhật ký lỗi đầy những con 500 sinh ra từ hoạt động bình thường,
/// nên một sự cố 500 THẬT không còn phân biệt được với một lần tra nhầm mã. Mọi phép đếm lỗi và
/// mọi ngưỡng cảnh báo dựng trên số 500 đều mất nghĩa.
///
/// Bộ test này là chốt giữ để nếu ai đó đảo hai dòng middleware đó về chỗ cũ thì có thứ báo động,
/// thay vì im lặng như lần trước.
/// </summary>
[Collection("Integration")]
public sealed class RequestLoggingLevelTests
{
    private readonly ApiFactory _factory;

    public RequestLoggingLevelTests(ApiFactory factory) => _factory = factory;

    /// <summary>
    /// Kiểm cả mã lẫn mức log trên cùng một request, vì lỗi cũ làm sai cả hai và sửa nửa vời thì
    /// vẫn còn nguyên vấn đề: 404 ghi ở mức Error vẫn làm nhiễu nhật ký lỗi y như cũ.
    /// </summary>
    private static void AssertLoggedAs(
        string pathFragment, string expectedStatusCode, LogEventLevel expectedLevel)
    {
        var completions = CapturingLogSink.RequestCompletionsFor(pathFragment);

        completions.Should().ContainSingle(
            $"phải có đúng một bản ghi kết thúc request cho {pathFragment}");

        CapturingLogSink.StatusCodeOf(completions[0]).Should().Be(expectedStatusCode,
            "nhật ký phải ghi mã mà người gọi thật sự nhận được");

        completions[0].Level.Should().Be(expectedLevel,
            "một tình huống nghiệp vụ bình thường không được nằm trong nhật ký lỗi — nếu không, " +
            "sự cố thật sẽ chìm giữa hàng nghìn dòng như thế này");
    }

    [Fact]
    public async Task ARecordThatDoesNotExist_IsLoggedAs404AtInformation()
    {
        // Chính request đã dùng để phát hiện lỗi: trước khi sửa in ra "[ERR] ... responded 500".
        CapturingLogSink.Clear();

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync("/api/v1/admin/users/999999");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound, "tiền đề: người gọi vẫn nhận 404");
        AssertLoggedAs("/api/v1/admin/users/999999", "404", LogEventLevel.Information);
    }

    [Fact]
    public async Task AnAuthorizationDenial_IsLoggedAs403NotAsAServerError()
    {
        // Từ chối quyền đi qua một nhánh ghi log riêng trong GlobalExceptionHandler (nhánh cố ý
        // tách ra để dò IDOR). Kiểm riêng vì nhánh đó dễ bị bỏ sót khi sửa.
        CapturingLogSink.Clear();

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
            .GetAsync($"/api/v1/analytics/shows/{SeedHelper.ShowId}/demand-forecast");

        res.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);

        var completions = CapturingLogSink.RequestCompletionsFor("demand-forecast");
        completions.Should().ContainSingle();
        CapturingLogSink.StatusCodeOf(completions[0]).Should().NotBe("500");
        completions[0].Level.Should().BeOneOf(LogEventLevel.Information, LogEventLevel.Warning);
    }

    [Fact]
    public async Task ABrokenBusinessRule_IsLoggedAsItsOwnCodeNotAs500()
    {
        // Vi phạm quy tắc nghiệp vụ trả 422. Đây là nhóm ngoại lệ đông nhất trong hệ thống, nên
        // cũng là nhóm làm nhiễu nhật ký nhiều nhất nếu bị ghi nhầm thành 500.
        CapturingLogSink.Clear();

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .PutAsJsonAsync("/api/v1/admin/system-config/platform_commission_rate",
                new { ConfigValue = "5", Note = "Giá trị vượt ngoài khoảng cho phép" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        AssertLoggedAs("platform_commission_rate", "422", LogEventLevel.Information);
    }

    [Fact]
    public async Task ARequestThatSucceeds_IsStillLoggedNormally()
    {
        // Kiểm chiều ngược lại: việc đổi thứ tự middleware không được làm mất bản ghi của những
        // request bình thường.
        CapturingLogSink.Clear();

        var res = await _factory.CreateClient().GetAsync("/api/v1/lounges?pageSize=5");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertLoggedAs("/api/v1/lounges", "200", LogEventLevel.Information);
    }
}
