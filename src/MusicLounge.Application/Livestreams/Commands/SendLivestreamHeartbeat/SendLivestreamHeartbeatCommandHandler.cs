using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Livestreams.Commands.SendLivestreamHeartbeat;

internal sealed class SendLivestreamHeartbeatCommandHandler
    : IRequestHandler<SendLivestreamHeartbeatCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SendLivestreamHeartbeatCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SendLivestreamHeartbeatCommand request, CancellationToken ct)
    {
        var sessionRepo = _uow.Repository<LivestreamViewingSession, int>();
        var matches = await sessionRepo.FindAsync(
            s => s.SessionId == request.SessionId && s.LivestreamId == request.LivestreamId, ct);
        var session = matches.FirstOrDefault()
            ?? throw new NotFoundException("LivestreamViewingSession", request.SessionId);

        // Vé thuộc đúng người gọi mới được heartbeat — SessionId là chuỗi Guid đủ khó đoán để không
        // cần thêm ràng buộc nào khác, nhưng kiểm tra chủ sở hữu vẫn là hàng phòng thủ bắt buộc.
        var ticket = await _uow.Repository<Ticket, Guid>().GetByIdAsync(session.TicketId, ct)
            ?? throw new NotFoundException(nameof(Ticket), session.TicketId);
        if (ticket.BuyerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền giữ phiên xem này.");

        session.LastHeartbeatAt = DateTimeOffset.UtcNow;
        sessionRepo.Update(session);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
