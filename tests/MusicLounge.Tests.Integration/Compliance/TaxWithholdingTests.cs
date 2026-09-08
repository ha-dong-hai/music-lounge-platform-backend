using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-289. The system withheld exactly one tax, at one rate, from everybody. NĐ 117/2025/NĐ-CP
/// asks two different questions of a platform that handles payment: which taxes (VAT *and* personal
/// income tax, 5% and 2% for services), and for whom (hộ/cá nhân kinh doanh only — a doanh nghiệp
/// declares its own, so withholding from one takes money the platform has no standing to take).
///
/// The seeded rate for personal income tax is 0, so shipping this moves no money on its own. These
/// tests turn it on to prove the whole path works, and turn it back off afterwards.
/// </summary>
[Collection("Integration")]
public sealed class TaxWithholdingTests
{
    private readonly ApiFactory _factory;

    public TaxWithholdingTests(ApiFactory factory) => _factory = factory;

    // ---------- helpers ----------

    private async Task SetConfigAsync(string key, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SystemConfigs.SingleAsync(c => c.ConfigKey == key)).ConfigValue = value;
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<ISystemConfigService>().Invalidate(key);
    }

    private async Task SetOwnerTaxProfileAsync(PayeeBusinessType? type, bool verified)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = await db.Users.SingleAsync(u => u.Id == SeedHelper.OwnerId);
        owner.BusinessType = type;
        owner.TaxProfileVerifiedAt = verified ? DateTimeOffset.UtcNow : null;
        owner.TaxProfileVerifiedBy = verified ? SeedHelper.AdminId : null;
        await db.SaveChangesAsync();
    }

    private async Task<int> SeedSellablePriceAsync(decimal price)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"TaxShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(6),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(6).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Standard",
            AccessType = AccessType.Physical, TotalCapacity = 50
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var ticketPrice = new TicketPrice
        {
            TierId = tier.Id, Name = "Regular", Price = price, Quota = 50, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
            SaleEnd = DateTimeOffset.UtcNow.AddDays(5),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(ticketPrice);
        await db.SaveChangesAsync();
        return ticketPrice.Id;
    }

    private async Task<(int PaymentId, Guid TicketId)> BuyOneTicketAsync(int priceId)
    {
        var buyer = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var hold = await (await buyer.PostAsJsonAsync("/api/v1/tickets/holds",
            new { PriceId = priceId, Quantity = 1 })).Content.ReadFromJsonAsync<Envelope<HoldData>>();
        var purchase = await (await buyer.PostAsJsonAsync("/api/v1/tickets/purchase",
            new { HoldId = hold!.Data.HoldId })).Content.ReadFromJsonAsync<Envelope<PurchaseData>>();

        await buyer.GetAsync(
            $"/api/v1/payments/vnpay/callback?vnp_TxnRef={purchase!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(purchase.Data.Amount * 100)}");

        // The callback redirects either way, success or failure, so the HTTP status says nothing.
        // What the tests below depend on is that the payment really was confirmed — that is the
        // moment the withholding is computed and the journal written.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Payments.SingleAsync(p => p.Id == purchase.Data.PaymentId)).Status
                .Should().Be(PaymentStatus.Confirmed);
        }

        return (purchase.Data.PaymentId, purchase.Data.TicketIds[0]);
    }

    /// <summary>Amount credited to a given ledger account by the journal for this payment.</summary>
    private static async Task<decimal> CreditedToAsync(
        ApplicationDbContext db, int paymentId, AccountType accountType, string referenceType)
    {
        var accountIds = await db.Set<Account>()
            .Where(a => a.OwnerType == accountType)
            .Select(a => a.Id)
            .ToListAsync();

        var entries = await db.LedgerEntries
            .Where(e => e.PaymentId == paymentId
                        && e.ReferenceType == referenceType
                        && accountIds.Contains(e.AccountId))
            .ToListAsync();

        return entries.Where(e => !e.IsDebit).Sum(e => e.Amount)
               - entries.Where(e => e.IsDebit).Sum(e => e.Amount);
    }

    // ---------- the arithmetic ----------

    [Theory]
    [InlineData(100_000, 0.05, 0.05, 0.02)]
    [InlineData(150_000, 0.05, 0.05, 0.00)]
    [InlineData(99_999, 0.033, 0.05, 0.02)]     // deliberately awkward: every part rounds
    [InlineData(1, 0.05, 0.05, 0.02)]           // smaller than one unit of any deduction
    public void EveryPartStillSumsToGross_NoMatterHowTheRatesRound(
        double gross, double commission, double vat, double pit)
    {
        // The ledger's debit-must-equal-credit invariant rests entirely on this. Adding a third
        // deduction is the kind of change that quietly breaks it, because rounding does not
        // distribute over addition — which is why owner net stays defined as the remainder rather
        // than being rounded in its own right.
        var fees = PaymentFeeCalculator.Split(
            (decimal)gross, (decimal)commission, (decimal)vat, (decimal)pit);

        (fees.PlatformFee + fees.Tax + fees.PersonalIncomeTax + fees.OwnerNet)
            .Should().Be((decimal)gross);
    }

    [Fact]
    public void PersonalIncomeTaxRate_IsSeededAtZero_SoShippingThisMovesNoMoney()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var seeded = db.SystemConfigs.Single(c => c.ConfigKey == ConfigKeys.PersonalIncomeTaxRate);

        seeded.ConfigValue.Should().Be("0",
            "turning withholding on takes money out of every household seller's share, so it has to " +
            "be a deliberate operational decision with a recorded reason — not a side effect of a " +
            "deployment going out");
        seeded.Description.Should().Contain("117/2025");
    }

    // ---------- who gets withheld from ----------

    [Fact]
    public async Task PersonalIncomeTax_LandsInItsOwnAccount_NotAddedToTheVatBalance()
    {
        await SetConfigAsync(ConfigKeys.PersonalIncomeTaxRate, "0.02");
        await SetOwnerTaxProfileAsync(PayeeBusinessType.HouseholdOrIndividual, verified: true);
        try
        {
            var priceId = await SeedSellablePriceAsync(200_000m);
            var (paymentId, _) = await BuyOneTicketAsync(priceId);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            (await CreditedToAsync(db, paymentId, AccountType.Tax, "payment"))
                .Should().Be(10_000m, "VAT is 5% of 200.000");
            (await CreditedToAsync(db, paymentId, AccountType.PersonalIncomeTax, "payment"))
                .Should().Be(4_000m,
                    "personal income tax is 2% of 200.000, and it is a different tax owed under " +
                    "different rules — a combined balance could not say how much of either is owed");

            var payment = await db.Payments.SingleAsync(p => p.Id == paymentId);
            payment.TaxWithheld.Should().Be(10_000m);
            payment.PersonalIncomeTaxWithheld.Should().Be(4_000m);
            payment.NetAmount.Should().Be(200_000m - 10_000m - 10_000m - 4_000m,
                "the owner receives what is left after commission and both taxes");

            var journal = await db.LedgerEntries
                .Where(e => e.PaymentId == paymentId && e.ReferenceType == "payment")
                .ToListAsync();
            journal.Where(e => e.IsDebit).Sum(e => e.Amount)
                .Should().Be(journal.Where(e => !e.IsDebit).Sum(e => e.Amount));
        }
        finally
        {
            await SetConfigAsync(ConfigKeys.PersonalIncomeTaxRate, "0");
            await SetOwnerTaxProfileAsync(null, verified: false);
        }
    }

    [Fact]
    public async Task VerifiedEnterprise_IsNotWithheldFromAtAll()
    {
        await SetConfigAsync(ConfigKeys.PersonalIncomeTaxRate, "0.02");
        await SetOwnerTaxProfileAsync(PayeeBusinessType.Enterprise, verified: true);
        try
        {
            var priceId = await SeedSellablePriceAsync(200_000m);
            var (paymentId, _) = await BuyOneTicketAsync(priceId);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            (await CreditedToAsync(db, paymentId, AccountType.Tax, "payment")).Should().Be(0m);
            (await CreditedToAsync(db, paymentId, AccountType.PersonalIncomeTax, "payment"))
                .Should().Be(0m);

            var payment = await db.Payments.SingleAsync(p => p.Id == paymentId);
            payment.NetAmount.Should().Be(200_000m - 10_000m,
                "an enterprise declares and pays its own tax, so only the platform's own commission " +
                "comes off — withholding here would be taking money the platform cannot remit for them");

            // The settlement must be sized off the same figure, or the payout and the ledger part ways.
            var scheduled = await db.Set<Settlement>()
                .Where(s => s.PaymentId == paymentId)
                .ToListAsync();
            scheduled.Sum(s => s.NetAmount).Should().Be(payment.NetAmount);
        }
        finally
        {
            await SetConfigAsync(ConfigKeys.PersonalIncomeTaxRate, "0");
            await SetOwnerTaxProfileAsync(null, verified: false);
        }
    }

    [Fact]
    public async Task DeclaringYourselfAnEnterprise_DoesNotStopWithholdingUntilSomeoneChecks()
    {
        // Otherwise the tax-profile endpoint is a self-service switch labelled "stop deducting tax
        // from me", which is not a claim a seller gets to make about themselves.
        await SetOwnerTaxProfileAsync(PayeeBusinessType.Enterprise, verified: false);
        try
        {
            var priceId = await SeedSellablePriceAsync(200_000m);
            var (paymentId, _) = await BuyOneTicketAsync(priceId);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            (await CreditedToAsync(db, paymentId, AccountType.Tax, "payment"))
                .Should().Be(10_000m, "an unverified declaration changes nothing");
        }
        finally
        {
            await SetOwnerTaxProfileAsync(null, verified: false);
        }
    }

    [Fact]
    public async Task UnclassifiedSeller_IsWithheldFrom_BecauseThatIsTheRecoverableMistake()
    {
        await SetOwnerTaxProfileAsync(null, verified: false);

        var priceId = await SeedSellablePriceAsync(200_000m);
        var (paymentId, _) = await BuyOneTicketAsync(priceId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await CreditedToAsync(db, paymentId, AccountType.Tax, "payment"))
            .Should().Be(10_000m,
                "over-withholding from an enterprise is a support ticket; under-withholding from a " +
                "household is an unpaid tax liability nobody is tracking");
    }

    // ---------- giving it back ----------

    [Fact]
    public async Task Refunding_GivesBackTheWithheldPersonalIncomeTaxToo()
    {
        await SetConfigAsync(ConfigKeys.PersonalIncomeTaxRate, "0.02");
        await SetOwnerTaxProfileAsync(PayeeBusinessType.HouseholdOrIndividual, verified: true);
        try
        {
            var priceId = await SeedSellablePriceAsync(200_000m);
            var (paymentId, ticketId) = await BuyOneTicketAsync(priceId);

            var buyer = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
            var cancel = await buyer.PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);
            cancel.StatusCode.Should().Be(HttpStatusCode.OK);
            var refundId = (await cancel.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;

            var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
            var process = await admin.PostAsJsonAsync(
                $"/api/v1/admin/refund-requests/{refundId}/process",
                new { Decision = "Approved", ApprovedAmount = 200_000m });
            process.StatusCode.Should().Be(HttpStatusCode.NoContent);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Debited back out of the tax accounts — the sign convention is why CreditedToAsync
            // returns credits minus debits, so a full reversal reads as the negative of the original.
            (await CreditedToAsync(db, paymentId, AccountType.PersonalIncomeTax, "refund"))
                .Should().Be(-4_000m,
                    "keeping the withholding would be wrong twice: the buyer is short, and the " +
                    "platform holds a deduction for revenue that no longer exists");
            (await CreditedToAsync(db, paymentId, AccountType.Tax, "refund")).Should().Be(-10_000m);

            var refundJournal = await db.LedgerEntries
                .Where(e => e.PaymentId == paymentId && e.ReferenceType == "refund")
                .ToListAsync();
            refundJournal.Where(e => e.IsDebit).Sum(e => e.Amount)
                .Should().Be(refundJournal.Where(e => !e.IsDebit).Sum(e => e.Amount));
        }
        finally
        {
            await SetConfigAsync(ConfigKeys.PersonalIncomeTaxRate, "0");
            await SetOwnerTaxProfileAsync(null, verified: false);
        }
    }

    // ---------- declaring ----------

    [Fact]
    public async Task OwnerCanDeclareTheirTaxProfile_AndIsToldWhetherWithholdingApplies()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        try
        {
            var res = await client.PutAsJsonAsync("/api/v1/me/tax-profile",
                new { BusinessType = "Enterprise", TaxCode = "0101243150" });
            res.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var profile = (await (await client.GetAsync("/api/v1/me/tax-profile"))
                .Content.ReadFromJsonAsync<Envelope<TaxProfile>>())!.Data;

            profile.BusinessType.Should().Be("Enterprise");
            profile.TaxCode.Should().Be("0101243150", "it is the seller's own tax code");
            profile.SubmittedAt.Should().NotBeNull();
            profile.VerifiedAt.Should().BeNull();
            profile.WithholdingApplies.Should().BeTrue();
            profile.Explanation.Should().Contain("chờ duyệt",
                "being withheld from while declared as an enterprise looks like a contradiction " +
                "unless the reason is stated");

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await db.Users.SingleAsync(u => u.Id == SeedHelper.OwnerId);
            stored.TaxCode.Should().NotBe("0101243150", "the tax code is encrypted at rest");
            stored.TaxCodeHash.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = await db.Users.SingleAsync(u => u.Id == SeedHelper.OwnerId);
            owner.BusinessType = null;
            owner.TaxCode = null;
            owner.TaxCodeHash = null;
            owner.TaxProfileSubmittedAt = null;
            owner.TaxProfileVerifiedAt = null;
            owner.TaxProfileVerifiedBy = null;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task RedeclaringAfterVerification_DropsTheVerification()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        try
        {
            await client.PutAsJsonAsync("/api/v1/me/tax-profile",
                new { BusinessType = "HouseholdOrIndividual", TaxCode = "8012345678" });
            await SetOwnerTaxProfileAsync(PayeeBusinessType.HouseholdOrIndividual, verified: true);

            // Approved as a household, now claiming to be a company. Carrying the old approval over
            // would let a seller switch their own withholding off through a checked front door.
            var res = await client.PutAsJsonAsync("/api/v1/me/tax-profile",
                new { BusinessType = "Enterprise", TaxCode = "8012345678" });
            res.StatusCode.Should().Be(HttpStatusCode.NoContent);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = await db.Users.SingleAsync(u => u.Id == SeedHelper.OwnerId);

            owner.BusinessType.Should().Be(PayeeBusinessType.Enterprise);
            owner.TaxProfileVerifiedAt.Should().BeNull();
            owner.TaxProfileVerifiedBy.Should().BeNull();
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = await db.Users.SingleAsync(u => u.Id == SeedHelper.OwnerId);
            owner.BusinessType = null;
            owner.TaxCode = null;
            owner.TaxCodeHash = null;
            owner.TaxProfileSubmittedAt = null;
            owner.TaxProfileVerifiedAt = null;
            owner.TaxProfileVerifiedBy = null;
            await db.SaveChangesAsync();
        }
    }

    [Theory]
    [InlineData("Enterprise", "12345", "10 chữ số")]
    [InlineData("Enterprise", "0101243150-01", "10 chữ số")]
    [InlineData("HoKinhDoanh", "0101243150", "Loại hình kinh doanh")]
    public async Task MalformedDeclaration_IsRejected(string businessType, string taxCode, string hint)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner")
            .PutAsJsonAsync("/api/v1/me/tax-profile", new { BusinessType = businessType, TaxCode = taxCode });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain(hint);
    }

    [Fact]
    public async Task CommissionPlusBothTaxes_CannotReachOneHundredPercent()
    {
        // The third deduction has to join the cross-key rule, not sit outside it: commission 50% +
        // VAT 45% + personal income tax 5% leaves the owner exactly nothing, and each rate on its
        // own looks perfectly reasonable.
        await SetConfigAsync(ConfigKeys.TaxRate, "0.45");
        try
        {
            var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
                .PutAsJsonAsync($"/api/v1/admin/system-config/{ConfigKeys.PersonalIncomeTaxRate}",
                    new { ConfigValue = "0.50", Note = "Thử đẩy tổng khấu trừ lên 100% để kiểm tra giới hạn" });

            res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await res.Content.ReadAsStringAsync()).Should().Contain("Sổ cái kép");
        }
        finally
        {
            await SetConfigAsync(ConfigKeys.TaxRate, "0.05");
        }
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record HoldData(int HoldId, DateTimeOffset ExpiresAt);
    private sealed record PurchaseData(
        int PaymentId, string OrderId, decimal Amount, string PaymentUrl, Guid[] TicketIds);
    private sealed record TaxProfile(
        string? BusinessType, string? TaxCode, DateTimeOffset? SubmittedAt, DateTimeOffset? VerifiedAt,
        bool WithholdingApplies, decimal VatRate, decimal PersonalIncomeTaxRate, string Explanation);
}
