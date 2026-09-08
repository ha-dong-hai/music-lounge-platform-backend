using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-301, closing a hole opened by MLACP-284 and MLACP-289 together.
///
/// Four rates cut the same donation gross: platform commission, VAT, personal income tax, and the
/// performer's share. The venue keeps whatever is left. Seeded, that is 2%.
///
/// MLACP-289 added personal income tax and documented 2% as the figure the decree specifies.
/// MLACP-284 made rates editable through an endpoint. Setting that 2% lands the venue's share on
/// exactly zero, and one more change after that makes it negative — at which point
/// ConfirmDonationPaid throws and the performer can never be paid, with the donor's money already
/// collected. The failure arrives days later, in a different flow, so it has to be refused on write.
///
/// The point is not to forbid withholding the tax. It is to make whoever enables it say whose share
/// the 2% comes out of.
/// </summary>
[Collection("Integration")]
public sealed class DonationSplitConfigGuardTests
{
    private readonly ApiFactory _factory;

    public DonationSplitConfigGuardTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<string> ValueOfAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.SystemConfigs.SingleAsync(c => c.ConfigKey == key)).ConfigValue;
    }

    private async Task RestoreAsync(string key, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SystemConfigs.SingleAsync(c => c.ConfigKey == key)).ConfigValue = value;
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<ISystemConfigService>().Invalidate(key);
    }

    private Task<HttpResponseMessage> SetAsync(string key, string value, string note = "Kiểm tra ràng buộc")
        => Admin().PutAsJsonAsync($"/api/v1/admin/system-config/{key}",
            new { ConfigValue = value, Note = note });

    [Fact]
    public async Task TurningOnPersonalIncomeTaxAtTheDecreeRate_IsRefusedWhileThePerformerShareIsStill88Percent()
    {
        // The exact action the system invites: 5% + 5% + 2% + 88% = 100%, venue keeps nothing.
        (await ValueOfAsync(ConfigKeys.DonationPerformerShareRate)).Should().Be("0.88",
            "test premise: the seeded performer share leaves only 2% of headroom");

        var res = await SetAsync(ConfigKeys.PersonalIncomeTaxRate, "0.02",
            "Bật khấu trừ TNCN theo NĐ 117/2025");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ValueOfAsync(ConfigKeys.PersonalIncomeTaxRate)).Should().Be("0",
            "a refused change must leave the live rate untouched");
    }

    [Fact]
    public async Task TheRefusal_SaysWhichKeyToLowerAndToWhat()
    {
        // Whoever is enabling this is meeting a legal obligation. "Invalid value" would leave them
        // stuck; the message has to name the trade-off they actually have to make.
        var res = await SetAsync(ConfigKeys.PersonalIncomeTaxRate, "0.02");

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain(ConfigKeys.DonationPerformerShareRate);
        body.Should().Contain("nghệ sĩ");
        body.Should().Contain("88", "1 − 5% − 5% − 2% = 88% is the ceiling it should quote");
    }

    [Fact]
    public async Task LoweringThePerformerShareFirst_ThenEnablingTheTax_Works()
    {
        // The whole point: the guard is not a wall, it is an ordering requirement.
        var originalShare = await ValueOfAsync(ConfigKeys.DonationPerformerShareRate);
        try
        {
            var lower = await SetAsync(ConfigKeys.DonationPerformerShareRate, "0.86",
                "Hạ phần nghệ sĩ để lấy chỗ cho khấu trừ TNCN");
            lower.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var enable = await SetAsync(ConfigKeys.PersonalIncomeTaxRate, "0.02",
                "Bật khấu trừ TNCN theo NĐ 117/2025");
            enable.StatusCode.Should().Be(HttpStatusCode.NoContent,
                "5% + 5% + 2% + 86% = 98%, so the venue still keeps 2%");
        }
        finally
        {
            await RestoreAsync(ConfigKeys.PersonalIncomeTaxRate, "0");
            await RestoreAsync(ConfigKeys.DonationPerformerShareRate, originalShare);
        }
    }

    [Fact]
    public async Task RaisingThePerformerShareBeyondWhatIsLeft_IsAlsoRefused()
    {
        // The constraint has to hold from whichever side it is approached.
        var res = await SetAsync(ConfigKeys.DonationPerformerShareRate, "0.95",
            "Nâng phần nghệ sĩ lên rất cao");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ValueOfAsync(ConfigKeys.DonationPerformerShareRate)).Should().Be("0.88");
    }

    [Fact]
    public async Task RaisingCommissionIntoThePerformerShare_IsRefusedToo()
    {
        // 10% + 5% + 0% + 88% = 103%. Before this change the old rule saw only 10% + 5% = 15% and
        // waved it through, because it had never heard of the performer share.
        var res = await SetAsync(ConfigKeys.PlatformCommissionRate, "0.10",
            "Nâng hoa hồng nền tảng lên 10%");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ValueOfAsync(ConfigKeys.PlatformCommissionRate)).Should().Be("0.05");
    }

    [Fact]
    public async Task AChangeThatLeavesTheVenueAMargin_IsStillAccepted()
    {
        var original = await ValueOfAsync(ConfigKeys.DonationPerformerShareRate);
        try
        {
            var res = await SetAsync(ConfigKeys.DonationPerformerShareRate, "0.80",
                "Giảm phần nghệ sĩ theo thoả thuận mới");
            res.StatusCode.Should().Be(HttpStatusCode.NoContent,
                "the guard must not block ordinary, safe adjustments");
        }
        finally
        {
            await RestoreAsync(ConfigKeys.DonationPerformerShareRate, original);
        }
    }
}
