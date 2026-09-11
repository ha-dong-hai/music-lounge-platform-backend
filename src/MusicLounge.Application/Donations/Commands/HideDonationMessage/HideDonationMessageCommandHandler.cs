using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Donations.Commands.HideDonationMessage;

/// <summary>
/// MLACP-360. Lời nhắn donate nay lên livestream ngay khi VNPay xác nhận. Bộ lọc từ cấm chỉ bắt được
/// những gì đã biết trước; phần còn lại do người đang điều hành buổi diễn gỡ tay — cùng cách YouTube
/// cho chủ kênh kiểm duyệt Super Chat "the same way you moderate Live chat messages".
///
/// <para>Ai được gỡ: đúng những người điều hành sàn diễn đó — chủ phòng trà, nhân viên được phân
/// công đúng phòng trà này, hoặc Admin. Nhân viên phòng trà khác thì không, dù cùng vai trò.</para>
///
/// <para>Gỡ lời nhắn không hoàn tiền: đó là donate tự nguyện đã được VNPay xác nhận, cái bị gỡ chỉ là
/// nội dung chữ. Lời nhắn gốc vẫn được giữ trong database, chỉ thôi hiển thị công khai, để còn đối
/// chiếu nếu người donate khiếu nại việc bị gỡ.</para>
/// </summary>
internal sealed class HideDonationMessageCommandHandler : IRequestHandler<HideDonationMessageCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ILivestreamHubService _hub;
    private readonly IAsyncKeyedLock _lock;
    private readonly ILogger<HideDonationMessageCommandHandler> _logger;

    public HideDonationMessageCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ILivestreamRepository livestreamRepo,
        ILivestreamHubService hub,
        IAsyncKeyedLock @lock,
        ILogger<HideDonationMessageCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _livestreamRepo = livestreamRepo;
        _hub = hub;
        _lock = @lock;
        _logger = logger;
    }

    public async Task<Unit> Handle(HideDonationMessageCommand request, CancellationToken ct)
    {
        // Cùng khoá với các lệnh đổi trạng thái donate khác, để hai người gỡ cùng lúc không ghi đè
        // người gỡ và thời điểm gỡ của nhau.
        await using var _ = await _lock.AcquireAsync($"donation:{request.DonationId}", ct);

        var donation = await _uow.Repository<Donation, int>().GetByIdAsync(request.DonationId, ct)
            ?? throw new NotFoundException(nameof(Donation), request.DonationId);
        var performance = await _uow.Repository<Performance, int>().GetByIdAsync(donation.PerformanceId, ct)
            ?? throw new NotFoundException(nameof(Performance), donation.PerformanceId);
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(performance.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), performance.LoungeShowId);
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        var isOwner = lounge.OwnerId == _currentUser.UserId;
        var isThisVenuesStaff = _currentUser.Role == Roles.Staff && _currentUser.LoungeId == lounge.Id;
        if (!isOwner && !isThisVenuesStaff && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException(
                "Chỉ chủ phòng trà, nhân viên của phòng trà này hoặc Admin mới gỡ được lời nhắn.");

        if (string.IsNullOrWhiteSpace(donation.Message))
            throw new DomainException("Donate này không có lời nhắn nào để gỡ.");

        // Gỡ lần nữa không đổi gì — giữ nguyên người gỡ và thời điểm gỡ đầu tiên.
        if (donation.MessageHiddenAt is not null) return Unit.Value;

        donation.MessageHiddenAt = DateTimeOffset.UtcNow;
        donation.MessageHiddenByUserId = _currentUser.UserId;
        _uow.Repository<Donation, int>().Update(donation);
        await DonationEvidence.AppendAsync(_uow, donation.Id, DonationEventType.MessageHidden,
            _currentUser.UserId, detail: "Gỡ lời nhắn khỏi livestream (không hoàn tiền).", ct: ct);
        await _uow.SaveChangesAsync(ct);

        var livestream = await _livestreamRepo.GetByShowIdAsync(show.Id, ct);
        if (livestream is not null)
        {
            try
            {
                await _hub.BroadcastDonationMessageHiddenAsync(livestream.Id, donation.Id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Việc gỡ đã được ghi: lời nhắn không còn trong bất kỳ danh sách công khai nào. Chỉ
                // những màn hình đang mở còn thấy nó cho tới khi tải lại.
                _logger.LogWarning(ex,
                    "Donation message hidden but live broadcast failed: DonationId={DonationId}", donation.Id);
            }
        }

        return Unit.Value;
    }
}
