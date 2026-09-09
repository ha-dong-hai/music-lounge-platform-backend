using MediatR;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Mutes.Commands.MuteLounge;

/// <summary>
/// MLACP-330. Đối xứng với <c>FollowLoungeCommandHandler</c>, ngược dấu.
///
/// <b>Tắt tiếng và theo dõi không thể cùng tồn tại.</b> Vừa theo dõi vừa tắt tiếng một phòng trà là
/// một trạng thái vô nghĩa, và tệ hơn là nó khiến hệ gợi ý nhận hai chỉ thị trái ngược cho cùng một
/// nơi. Nên bấm tắt tiếng thì bỏ theo dõi luôn — người dùng vừa nói rõ ý họ, hệ thống không việc gì
/// phải hỏi lại.
/// </summary>
internal sealed class MuteLoungeCommandHandler : ICommandHandler<MuteLoungeCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public MuteLoungeCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(MuteLoungeCommand request, CancellationToken ct)
    {
        var loungeExists = await _uow.Repository<Domain.Entities.MusicLounge, int>()
            .AnyAsync(l => l.Id == request.LoungeId, ct);

        if (!loungeExists)
            throw new NotFoundException("Lounge", request.LoungeId);

        var userId = _currentUser.UserId;

        var alreadyMuted = await _uow.Repository<LoungeMute, int>()
            .AnyAsync(m => m.UserId == userId && m.LoungeId == request.LoungeId, ct);

        if (alreadyMuted)
            throw new ConflictException("Bạn đã tắt tiếng phòng trà này.");

        // Gỡ theo dõi nếu có: hai chỉ thị trái ngược cho cùng một phòng trà không được cùng tồn tại.
        var follows = await _uow.Repository<Follow, int>()
            .FindAsync(f => f.UserId == userId && f.LoungeId == request.LoungeId, ct);
        foreach (var follow in follows)
            _uow.Repository<Follow, int>().Remove(follow);

        _uow.Repository<LoungeMute, int>().Add(new LoungeMute
        {
            UserId = userId,
            LoungeId = request.LoungeId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
