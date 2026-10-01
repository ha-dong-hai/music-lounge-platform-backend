using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Complaints;

/// <summary>
/// MLACP-515. Trước khi đổi khoá sang GUID, TargetId của khiếu nại là int còn Ticket.Id đã là Guid, nên khiếu nại loại
/// "ticket" không kiểm được đối tượng và nhận MỌI số — kể cả số không trỏ tới vé nào. Nay cùng kiểu: vé không tồn tại
/// phải bị từ chối như show/venue/donation.
/// </summary>
[Collection("Integration")]
public sealed class TicketComplaintTargetTests
{
    private readonly ApiFactory _factory;

    public TicketComplaintTargetTests(ApiFactory factory) => _factory = factory;

    private Task<HttpResponseMessage> KhieuNaiVeAsync(Guid ticketId) =>
        _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience").PostAsJsonAsync("/api/v1/complaints", new
        {
            TargetType = "ticket", TargetId = ticketId, Category = "Other",
            Description = "Vé của tôi không quét được ở cửa", EvidenceUrls = (string?)null, ContactPhone = (string?)null
        });

    [Fact]
    public async Task VeKhongTonTai_BiTuChoi()
        => (await KhieuNaiVeAsync(Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.BadRequest);

    [Fact]
    public async Task VeCoThat_DuocNhan()
        => (await KhieuNaiVeAsync(SeedHelper.AudienceTicketId)).StatusCode.Should().Be(HttpStatusCode.Created);
}
