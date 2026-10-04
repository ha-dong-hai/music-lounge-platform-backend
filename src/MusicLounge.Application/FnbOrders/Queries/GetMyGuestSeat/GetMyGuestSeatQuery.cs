using MediatR;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.FnbOrders.Queries.GetMyGuestSeat;

/// <summary>MLACP-630: khu mà người đang đăng nhập ngồi ở phòng trà này (theo vé của họ), null nếu không suy ra được —
/// xem <see cref="GuestSeat"/>.</summary>
public sealed record GetMyGuestSeatQuery(Guid LoungeId) : IQuery<GuestSeatDto?>;

internal sealed class GetMyGuestSeatQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    : IRequestHandler<GetMyGuestSeatQuery, GuestSeatDto?>
{
    public Task<GuestSeatDto?> Handle(GetMyGuestSeatQuery request, CancellationToken ct)
        => GuestSeat.ResolveAsync(uow, currentUser.UserId, request.LoungeId, DateTimeOffset.UtcNow, ct);
}
