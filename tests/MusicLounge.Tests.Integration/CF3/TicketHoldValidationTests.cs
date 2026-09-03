using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF3;

/// <summary>
/// POST /api/v1/tickets/holds — HoldTicketCommandValidator now checks PriceId existence up front
/// (master-backend-techlead review — FK existence checks belong in the Validator, not discovered
/// deep in the handler).
/// </summary>
[Collection("Integration")]
public sealed class TicketHoldValidationTests
{
    private readonly ApiFactory _factory;

    public TicketHoldValidationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Hold_NonExistentPriceId_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/tickets/holds", new
        {
            PriceId = 999_999_999,
            Quantity = 1
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
