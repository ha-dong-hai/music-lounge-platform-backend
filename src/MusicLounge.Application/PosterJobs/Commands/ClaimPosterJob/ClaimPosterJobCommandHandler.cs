using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.PosterJobs.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.PosterJobs.Commands.ClaimPosterJob;

/// <summary>
/// MLACP-458. Giao đơn cũ nhất cho máy trạm và khoá nó lại bằng một hạn thuê.
///
/// Khoá <see cref="IAsyncKeyedLock"/> để hai máy trạm không cùng nhận một đơn. Nói thẳng: bộ test của dự án chạy tuần tự
/// trên TestServer nên KHÔNG chứng minh được điều đó — khoá ở đây là theo đúng nếp sẵn có của các handler tranh chấp
/// trong dự án, không phải vì đã có test chứng minh.
/// </summary>
internal sealed class ClaimPosterJobCommandHandler : IRequestHandler<ClaimPosterJobCommand, PosterJobDto?>
{
    private readonly IUnitOfWork _uow;
    private readonly IAsyncKeyedLock _lock;

    public ClaimPosterJobCommandHandler(IUnitOfWork uow, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _lock = @lock;
    }

    public async Task<PosterJobDto?> Handle(ClaimPosterJobCommand request, CancellationToken ct)
    {
        await using var _ = await _lock.AcquireAsync("poster-job-claim", ct);

        var repo = _uow.Repository<AiPosterGeneration, int>();
        var dangCho = await repo.FindAsync(g => g.Status == AiPosterGenerationStatus.Queued, ct);

        // Cũ nhất trước: ai bấm trước được phục vụ trước. Sắp xếp ở phía ứng dụng vì hàng đợi này nhỏ (mỗi buổi hòa nhạc
        // chỉ được có một đơn đang chờ, và hạn mức tháng của từng chủ phòng trà chặn ở đầu vào).
        var job = dangCho.OrderBy(g => g.CreatedAt).ThenBy(g => g.Id).FirstOrDefault();
        if (job is null) return null;

        var now = DateTimeOffset.UtcNow;
        job.Status = AiPosterGenerationStatus.Rendering;
        job.ClaimedBy = request.WorkerId;
        job.ClaimedAt = now;
        job.LeaseExpiresAt = now + PosterQueue.Lease;
        job.AttemptCount++;
        repo.Update(job);
        await _uow.SaveChangesAsync(ct);

        return new PosterJobDto(job.Id, job.ShowId, job.Prompt, PosterQueue.AspectRatio, job.AttemptCount);
    }
}
