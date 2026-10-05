using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Admin;

/// <summary>
/// MLACP-617. Danh sach khieu nai cua Admin phai tra han xu ly cua tung dong — khong co no thi giao dien khong the noi
/// dong nao da qua han, trong khi huy hieu menu (work-queue) da dem duoc so qua han.
/// </summary>
[Collection("Integration")]
public sealed class ComplaintSlaDeadlineInListTests
{
    private readonly ApiFactory _factory;

    public ComplaintSlaDeadlineInListTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DanhSachKhieuNai_TraHanXuLyCuaTungDong()
    {
        var han = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        Guid id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var c = new Complaint
            {
                ComplainantUserId = SeedHelper.AudienceId, TargetType = "show", TargetId = SeedHelper.ShowId,
                Category = ComplaintCategory.Other, Description = $"han-{Guid.NewGuid():N}", Status = ComplaintStatus.Open,
                CreatedAt = han.AddHours(-48), SlaDeadline = han
            };
            db.Add(c);
            await db.SaveChangesAsync();
            id = c.Id;
        }

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync("/api/v1/admin/complaints?status=Open&pageSize=100");
        res.EnsureSuccessStatusCode();
        var dong = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("items")
            .EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);

        dong.GetProperty("slaDeadline").GetDateTimeOffset().Should().Be(han);
    }
}
