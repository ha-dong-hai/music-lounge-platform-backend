using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Commands.LogUserBehaviour;

public sealed class LogUserBehaviourJob
{
    // EMA: weight_new = LearningRate * signal + (1 - LearningRate) * weight_old — cung cong thuc
    // da duoc ghi trong comment cua UserCustomPreference.Weight, uu tien tin hieu hanh vi gan day
    // hon so voi trong so cu.
    private const decimal LearningRate = 0.3m;
    private const decimal PurchaseSignal = 1.0m;
    private const decimal DefaultWeight = 0.5m;

    private readonly IRepository<UserBehaviourLog, int> _logRepo;
    private readonly IRepository<User, int> _userRepo;
    private readonly IRepository<EventCustomValue, int> _eventValueRepo;
    private readonly IRepository<UserCustomPreference, int> _preferenceRepo;
    private readonly IUnitOfWork _uow;

    public LogUserBehaviourJob(
        IRepository<UserBehaviourLog, int> logRepo,
        IRepository<User, int> userRepo,
        IRepository<EventCustomValue, int> eventValueRepo,
        IRepository<UserCustomPreference, int> preferenceRepo,
        IUnitOfWork uow)
    {
        _logRepo = logRepo;
        _userRepo = userRepo;
        _eventValueRepo = eventValueRepo;
        _preferenceRepo = preferenceRepo;
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

        // MLACP-219: dat ve la tin hieu "thich" manh nhat trong cac BehaviourAction hien co, nen chi
        // hoc trong so tu hanh vi nay (khong phai moi action) — cac action con lai (xem, tim kiem...)
        // van chi duoc ghi vao user_behaviour_log nhu truoc, roi RecomputeUserEventScoresJob tong hop
        // rieng cho collab_score.
        if (action == BehaviourAction.PurchaseTicket)
            await UpdateCustomPreferenceWeightsAsync(userId, showId);

        await _uow.SaveChangesAsync();
    }

    private async Task UpdateCustomPreferenceWeightsAsync(int userId, int showId)
    {
        var eventValues = await _eventValueRepo.FindAsync(v => v.ShowId == showId);
        if (eventValues.Count == 0) return;

        var criteriaIds = eventValues.Select(v => v.CriteriaId).ToList();
        var existingPrefs = await _preferenceRepo.FindAsync(
            p => p.UserId == userId && criteriaIds.Contains(p.CriteriaId));
        var prefByCriteria = existingPrefs.ToDictionary(p => p.CriteriaId);

        var now = DateTimeOffset.UtcNow;
        foreach (var eventValue in eventValues)
        {
            if (prefByCriteria.TryGetValue(eventValue.CriteriaId, out var pref))
            {
                // Khong ghi de so thich nguoi dung tu tay chinh (Source=Explicit) bang tin hieu
                // hoc tu hanh vi — chi cap nhat/tao moi cac dong da la Learned tu truoc.
                if (pref.Source == CustomPreferenceSource.Explicit) continue;

                pref.Weight = LearningRate * PurchaseSignal + (1 - LearningRate) * pref.Weight;
                pref.Value = eventValue.Value;
                pref.UpdatedAt = now;
                _preferenceRepo.Update(pref);
            }
            else
            {
                _preferenceRepo.Add(new UserCustomPreference
                {
                    UserId = userId,
                    CriteriaId = eventValue.CriteriaId,
                    Value = eventValue.Value,
                    Source = CustomPreferenceSource.Learned,
                    Weight = LearningRate * PurchaseSignal + (1 - LearningRate) * DefaultWeight,
                    UpdatedAt = now
                });
            }
        }
    }
}
