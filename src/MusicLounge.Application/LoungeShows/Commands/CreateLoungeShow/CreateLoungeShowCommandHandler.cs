using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.CreateLoungeShow;

internal sealed class CreateLoungeShowCommandHandler : IRequestHandler<CreateLoungeShowCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateLoungeShowCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateLoungeShowCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền tạo event cho venue này.");

        // D14: can co goi subscription Active tai thoi diem tao event (khong phai luc publish);
        // show da Published truoc do khong bi anh huong neu subscription het han sau nay.
        // Filter Status server-side, loc ExpiresAt client-side - ket hop enum equality voi
        // DateTimeOffset comparison trong 1 query khong dich duoc sang SQLite (provider dung trong test).
        var now = DateTimeOffset.UtcNow;
        var activeStatusSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
            s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active, ct);
        var hasActiveSubscription = activeStatusSubs.Any(s => s.ExpiresAt > now);

        if (!hasActiveSubscription)
            throw new DomainException(
                "Bạn cần có gói subscription đang hoạt động để tạo event mới. Vui lòng đăng ký gói.");

        var format = Enum.Parse<LoungeShowFormat>(request.Format, ignoreCase: true);

        var show = new LoungeShow
        {
            LoungeId = request.LoungeId,
            Name = request.Name,
            Description = request.Description,
            Format = format,
            Status = LoungeShowStatus.Draft,
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = request.ScheduledEnd,
            CategoryId = request.CategoryId,
            OfflineQuota = request.OfflineQuota,
            OnlineQuota = request.OnlineQuota
        };

        _uow.Repository<LoungeShow, int>().Add(show);
        await _uow.SaveChangesAsync(ct);

        var performerRepo = _uow.Repository<Performer, int>();

        foreach (var input in request.Performances)
        {
            int performerId;
            if (input.PerformerId.HasValue)
            {
                var performer = await performerRepo.GetByIdAsync(input.PerformerId.Value, ct)
                    ?? throw new NotFoundException(nameof(Performer), input.PerformerId.Value);
                performerId = performer.Id;
            }
            else
            {
                var newPerformer = new Performer
                {
                    Name = input.PerformerName!,
                    CreatedByUserId = _currentUser.UserId,
                    CreatedAt = DateTime.UtcNow
                };
                performerRepo.Add(newPerformer);
                await _uow.SaveChangesAsync(ct);
                performerId = newPerformer.Id;
            }

            var role = Enum.Parse<PerformerRole>(input.Role, ignoreCase: true);
            _uow.Repository<Performance, int>().Add(new Performance
            {
                LoungeShowId = show.Id,
                PerformerId = performerId,
                Role = role,
                OrderIndex = input.OrderIndex,
                SetTime = input.SetTime,
                AcceptsDonation = input.AcceptsDonation
            });
        }

        foreach (var genreId in request.GenreIds.Distinct())
        {
            _uow.Repository<LoungeShowGenre, int>().Add(new LoungeShowGenre
            {
                LoungeShowId = show.Id,
                GenreId = genreId
            });
        }

        await _uow.SaveChangesAsync(ct);

        return show.Id;
    }
}
