using FluentAssertions;
using MusicLounge.Infrastructure.Hubs;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// MLACP-646. Đo 05/10/2026: 5 khán giả xem mà đỉnh ghi 7, tổng lượt 8 — nhân viên và Admin đang giám sát bị đếm như
/// khán giả. Quy tắc nằm ở <see cref="LivestreamHub.CountsAsAudience"/>, OnConnectedAsync bỏ qua việc đếm khi nó trả false.
///
/// <para>Giới hạn: bộ test không có SignalR client nên không nối hub thật được — phần OnConnectedAsync gọi quy tắc này
/// được kiểm bằng Playwright trên máy cục bộ (Admin + nhân viên cùng vào, số "người đang xem" chỉ bằng số khán giả).</para>
/// </summary>
public sealed class ViewerCountAudienceOnlyTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void OnlyTheAudienceIsCounted(bool isAdmin, bool isVenueOperator, bool counted)
        => LivestreamHub.CountsAsAudience(isAdmin, isVenueOperator).Should().Be(counted);
}
