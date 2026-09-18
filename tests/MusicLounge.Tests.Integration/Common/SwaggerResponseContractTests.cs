using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using MusicLounge.Api.Controllers;
using MusicLounge.Application.Common.Models;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Common;

/// <summary>
/// MLACP-453. Ba endpoint khai với Swagger là trả kiểu trần (<c>IReadOnlyList&lt;SystemConfigDto&gt;</c>…) trong khi thực tế
/// trả <c>{success, data, message}</c> — client sinh tự động từ Swagger sẽ ra kiểu sai. Test đọc đúng nguồn Swagger dùng để
/// sinh đặc tả (ApiExplorer), nên thêm endpoint mới mà khai sai là hỏng build.
///
/// Giới hạn: test kiểm chiều "khai kiểu trần". Chiều ngược — khai <c>ApiResponse&lt;T&gt;</c> nhưng thực tế trả trần — không
/// kiểm được bằng metadata, vì phải chạy từng action mới biết nó trả gì.
/// </summary>
[Collection("Integration")]
public sealed class SwaggerResponseContractTests
{
    /// <summary>
    /// Ngoại lệ duy nhất, có lý do: VNPay quy định cứng định dạng phản hồi IPN (<c>{"RspCode","Message"}</c>). Bọc nó vào
    /// <c>ApiResponse</c> thì VNPay không đọc được và sẽ gọi lại IPN tới hết lượt thử.
    /// </summary>
    private static readonly HashSet<Type> NgoaiLe = [typeof(VnPayIpnResponse)];

    private readonly ApiFactory _factory;

    public SwaggerResponseContractTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void MoiKieuPhanHoi2xxKhaiVoiSwagger_DeuBocApiResponse()
    {
        var provider = _factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();
        var moiKhaiBao = provider.ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .SelectMany(d => d.SupportedResponseTypes
                .Where(r => r.StatusCode is 200 or 201 && r.Type is not null && r.Type != typeof(void))
                .Select(r => (Endpoint: $"{d.HttpMethod} /{d.RelativePath}", Kieu: r.Type!)))
            .ToList();

        moiKhaiBao.Should().HaveCountGreaterThan(100, "bản thân phép quét phải không được im lặng khớp số không");

        var sai = moiKhaiBao
            .Where(x => !(x.Kieu.IsGenericType && x.Kieu.GetGenericTypeDefinition() == typeof(ApiResponse<>))
                        && !NgoaiLe.Contains(x.Kieu))
            .Select(x => $"{x.Endpoint} khai {x.Kieu.Name}")
            .OrderBy(s => s)
            .ToList();

        string.Join(Environment.NewLine, sai).Should().BeEmpty(
            "mọi phản hồi JSON đều bọc {success, data, message}; khai kiểu trần với Swagger là nói sai với client sinh tự động. " +
            "Dùng [ProducesResponseType<ApiResponse<T>>]");
    }

    [Fact]
    public async Task CauHinhAdmin_TraDungVoBocApiResponse()
    {
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        foreach (var url in new[] { "/api/v1/admin/system-config", "/api/v1/admin/system-config/ticket_hold_minutes/history" })
        {
            var res = await admin.GetAsync(url);
            res.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var goc = doc.RootElement;
            goc.GetProperty("success").GetBoolean().Should().BeTrue(url);
            goc.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array, url);
            goc.TryGetProperty("message", out _).Should().BeTrue($"{url}: cùng hình dạng với mọi endpoint khác, có khoá message");
        }
    }
}
