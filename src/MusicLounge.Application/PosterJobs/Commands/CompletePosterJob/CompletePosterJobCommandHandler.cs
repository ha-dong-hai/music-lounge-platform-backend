using MusicLounge.Domain.ValueObjects;
using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Notifications;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.PosterJobs.Commands.CompletePosterJob;

/// <summary>
/// MLACP-458. Nhận ảnh từ máy trạm, lưu, gắn vào buổi hòa nhạc, báo chủ phòng trà.
///
/// Từ đây trở đi mọi thứ giống hệt đường gọi thẳng (cùng <see cref="IFileStorageService"/>, cùng cách đặt tên file theo
/// định dạng thật của ảnh như MLACP-421 đã sửa) — chỉ khác ở chỗ ảnh đến từ đâu.
/// </summary>
internal sealed class CompletePosterJobCommandHandler : IRequestHandler<CompletePosterJobCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly IFileStorageService _fileStorage;
    private readonly INotificationService _notifications;

    public CompletePosterJobCommandHandler(
        IUnitOfWork uow, IFileStorageService fileStorage, INotificationService notifications)
    {
        _uow = uow;
        _fileStorage = fileStorage;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(CompletePosterJobCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<AiPosterGeneration, int>();
        var job = await repo.GetByIdAsync(request.JobId, ct)
            ?? throw new NotFoundException(nameof(AiPosterGeneration), request.JobId);

        // Máy trạm cũ nộp bài muộn thì bị từ chối ở đây: sau khi hết hạn thuê, đơn đã được trả về hàng đợi và có thể đang
        // do máy khác làm — nhận cả hai bài nộp sẽ ghi đè poster một cách ngẫu nhiên theo thứ tự về đích.
        if (job.Status != AiPosterGenerationStatus.Rendering || job.ClaimedBy != request.WorkerId)
            throw new ConflictException(
                "Đơn tạo poster này không còn do máy trạm của bạn giữ (có thể đã hết hạn và được giao lại).");

        // MLACP-421: đặt tên file theo ĐỊNH DẠNG THẬT của ảnh — bộ kiểm khi lưu đối chiếu phần mở rộng với chữ ký file.
        var tenFile = ImageMimeTypeHelper.FromContent(request.Content) switch
        {
            "image/png" => "poster.png",
            "image/jpeg" => "poster.jpg",
            "image/webp" => "poster.webp",
            "image/gif" => "poster.gif",
            _ => throw new DomainException("Nội dung tải lên không phải là ảnh nhận dạng được.")
        };

        string imageUrl;
        await using (var stream = new MemoryStream(request.Content))
        {
            imageUrl = await _fileStorage.SaveImageAsync(stream, tenFile, ct);
        }

        var now = DateTimeOffset.UtcNow;
        job.Status = AiPosterGenerationStatus.Succeeded;
        job.ImageUrl = imageUrl;
        job.LeaseExpiresAt = null;
        repo.Update(job);

        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(job.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), job.ShowId);
        show.PosterUrl = imageUrl;
        show.PosterByAi = true;
        showRepo.Update(show);

        // Chủ phòng trà đã rời màn hình từ lâu (một lượt mất hàng phút), nên thông báo là đường duy nhất họ biết kết quả.
        // Gọi TRƯỚC SaveChangesAsync: NotifyAsync chỉ xếp hàng bản ghi (giống ILedgerService.WriteJournalAsync), chính
        // SaveChangesAsync của handler mới ghi xuống — nên thông báo và tấm poster cùng sống hoặc cùng không, không bao
        // giờ có chuyện báo "đã xong" cho một poster chưa lưu được.
        await _notifications.NotifyAsync(
            job.OwnerId,
            NotificationType.PosterGenerationResult,
            new SongNgu(
                "Poster đã tạo xong",
                "Your poster is ready"),
            new SongNgu(
                $"Poster cho buổi hòa nhạc \"{show.Name}\" đã được tạo xong.",
                $"The poster for the concert \"{show.Name}\" has been created."),
            NotificationReferenceTypes.Show,
            show.Id.ToString(),
            ct);

        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
