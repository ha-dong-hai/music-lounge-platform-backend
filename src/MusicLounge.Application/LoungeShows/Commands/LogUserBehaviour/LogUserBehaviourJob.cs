using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Commands.LogUserBehaviour;

public sealed class LogUserBehaviourJob
{
    private readonly IRepository<UserBehaviourLog, int> _logRepo;
    private readonly IRepository<User, int> _userRepo;
    private readonly IUnitOfWork _uow;

    public LogUserBehaviourJob(
        IRepository<UserBehaviourLog, int> logRepo,
        IRepository<User, int> userRepo,
        IUnitOfWork uow)
    {
        _logRepo = logRepo;
        _userRepo = userRepo;
        _uow = uow;
    }

    public async Task ExecuteAsync(int userId, int showId, BehaviourAction action)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null || !user.AiConsent) return;

        _logRepo.Add(new UserBehaviourLog
        {
            UserId = userId,
            LoungeShowId = showId,
            Action = action,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _uow.SaveChangesAsync();
    }
}
