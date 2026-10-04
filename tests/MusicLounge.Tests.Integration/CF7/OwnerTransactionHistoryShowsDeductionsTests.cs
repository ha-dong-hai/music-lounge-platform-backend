using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF7;

/// <summary>
/// MLACP-616. Lich su giao dich cua chu phong tra la so chi tiet tai khoan rieng cua ho tren so cai. Truoc day no chi
/// lay dong ghi Co, nen khi mot ve DA giai ngan roi moi bi hoan — he thong thu hoi tien tu tai khoan chu phong tra
/// (ghi No) — khoan thu hoi do khong hien: sao ke luon ghi nhieu hon so chu that su con giu.
/// So chi tiet tai khoan phai co ca phat sinh No lan Co; khoan giam mang so am (quy uoc sao ke ngan hang).
/// </summary>
[Collection("Integration")]
public sealed class OwnerTransactionHistoryShowsDeductionsTests
{
    private readonly ApiFactory _factory;

    public OwnerTransactionHistoryShowsDeductionsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task KhoanBiThuHoiSauGiaiNgan_HienTrongLichSu_VoiSoAm()
    {
        var maGiaiNgan = $"giai-ngan-{Guid.NewGuid():N}";
        var maThuHoi = $"thu-hoi-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var ledger = scope.ServiceProvider.GetRequiredService<ILedgerService>();
            await ledger.WriteJournalAsync(Guid.NewGuid().ToString("N"), LedgerReferenceTypes.Settlement, maGiaiNgan, null,
            [
                new(AccountType.Platform, null, 900_000m, IsDebit: true, Description: "giải ngân"),
                new(AccountType.User, SeedHelper.OwnerId, 900_000m, IsDebit: false, Description: "giải ngân")
            ]);
            await ledger.WriteJournalAsync(Guid.NewGuid().ToString("N"), LedgerReferenceTypes.Refund, maThuHoi, null,
            [
                new(AccountType.User, SeedHelper.OwnerId, 300_000m, IsDebit: true, Description: "thu hồi phần đã giải ngân"),
                new(AccountType.Gateway, null, 300_000m, IsDebit: false, Description: "hoàn cho khách")
            ]);
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();
        }

        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var res = await owner.GetAsync("/api/v1/me/transactions?pageSize=100");
        res.EnsureSuccessStatusCode();
        var items = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("items")
            .EnumerateArray().ToList();

        var maCo = items.Select(i => i.GetProperty("referenceId").GetString()).ToList();
        maCo.Should().Contain(maGiaiNgan, "khoản giải ngân (ghi Có) luôn hiện");
        maCo.Should().Contain(maThuHoi, "khoản bị thu hồi (ghi Nợ) cũng phải hiện");

        decimal SoTien(string ma) => items.Single(i => i.GetProperty("referenceId").GetString() == ma)
            .GetProperty("amount").GetDecimal();

        SoTien(maGiaiNgan).Should().Be(900_000m);
        SoTien(maThuHoi).Should().Be(-300_000m, "khoản bị thu hồi phải hiện, và là khoản giảm");
    }
}
