using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Livestreams.Commands.CreateLivestream;

internal sealed class CreateLivestreamCommandHandler : IRequestHandler<CreateLivestreamCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ILivestreamServiceFactory _factory;
    private readonly ICurrentUserService _currentUser;

    public CreateLivestreamCommandHandler(
        IUnitOfWork uow,
        ILivestreamRepository livestreamRepo,
        ILivestreamServiceFactory factory,
        ICurrentUserService currentUser)
    {
        _uow = uow;
        _livestreamRepo = livestreamRepo;
        _factory = factory;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateLivestreamCommand request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);
        if (!VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, lounge.OwnerId))
            throw new ForbiddenException("Bạn không có quyền tạo livestream cho venue này.");

        if (show.Format == LoungeShowFormat.Offline)
            throw new DomainException("Offline shows cannot have a livestream.");

        // D15: livestream must exist BEFORE the show is submitted for approval, so Draft/Pending
        // shows are allowed here too — only block shows that are already over.
        if (show.Status is LoungeShowStatus.Cancelled or LoungeShowStatus.Ended)
            throw new DomainException("Không thể tạo livestream cho show đã hủy hoặc đã kết thúc.");

        var existing = await _livestreamRepo.GetByShowIdAsync(request.ShowId, ct);
        if (existing is not null)
            throw new ConflictException($"A livestream already exists for show {request.ShowId}.");

        var provider = _factory.GetProvider();
        var result = await provider.CreateStreamAsync(show.Name, ct);

        var livestream = new Livestream
        {
            LoungeShowId = request.ShowId,
            Provider = _factory.ActiveProviderKey,
            ProviderRef = result.ProviderRef,
            RtmpUrl = result.RtmpUrl,
            StreamKey = result.StreamKey,
            HlsUrl = result.HlsUrl,
            Status = LivestreamStatus.Scheduled
        };

        _uow.Repository<Livestream, int>().Add(livestream);
        await _uow.SaveChangesAsync(ct);

        // Create moderation record for Admin to review before going live (W08)
        _uow.Repository<EventModeration, int>().Add(new EventModeration
        {
            TargetType = ModerationTargetType.Livestream,
            TargetId = livestream.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _uow.SaveChangesAsync(ct);

        return livestream.Id;
    }
}
