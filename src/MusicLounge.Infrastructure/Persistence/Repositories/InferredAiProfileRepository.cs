using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class InferredAiProfileRepository : IInferredAiProfileRepository
{
    private readonly ApplicationDbContext _ctx;

    public InferredAiProfileRepository(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task ForgetAsync(int userId, CancellationToken ct = default)
    {
        // Diem so hanh vi theo tung buoi dien. Khoa kep nen khong nam sau repository chung duoc —
        // va do la ly do no da bi bo sot o duong xoa du lieu ca nhan.
        var scores = await _ctx.Set<UserEventScore>()
            .Where(s => s.UserId == userId).ToListAsync(ct);
        _ctx.Set<UserEventScore>().RemoveRange(scores);

        // Ket qua goi y da tinh san.
        var recommendations = await _ctx.Set<AiRecommendation>()
            .Where(r => r.UserId == userId).ToListAsync(ct);
        _ctx.Set<AiRecommendation>().RemoveRange(recommendations);

        // Trong so tieu chi rieng, duoc LogUserBehaviourJob suy ra tu hanh vi bang trung binh dong.
        // Khong co duong nao cho nguoi dung tu khai chung, nen toan bo la du lieu suy dien.
        var preferences = await _ctx.Set<UserCustomPreference>()
            .Where(p => p.UserId == userId).ToListAsync(ct);
        _ctx.Set<UserCustomPreference>().RemoveRange(preferences);
    }
}
