using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Commands.CancelHold;

/// <summary>
/// Bỏ giữ chỗ vé khi người mua đổi ý. Trước đây chỉ có đường tạo giữ chỗ, không có đường bỏ — nên
/// ghế bị treo cho tới khi job dọn hết hạn chạy, dù người mua đã rời đi từ lâu và người khác đang
/// muốn mua đúng ghế đó.
/// </summary>
internal sealed class CancelHoldCommandHandler : IRequestHandler<CancelHoldCommand, bool>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAsyncKeyedLock _lock;

    public CancelHoldCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _lock = @lock;
    }

    public async Task<bool> Handle(CancelHoldCommand request, CancellationToken ct)
    {
        // Cùng khoá mà PurchaseTicketCommandHandler dùng. Không có nó, một lượt huỷ chạy song song
        // với một lượt mua trên cùng giữ chỗ có thể xoá bản ghi TicketHold ngay dưới chân lượt mua
        // đang dở, thay vì một trong hai thất bại sạch sẽ trước.
        await using var _ = await _lock.AcquireAsync($"purchase-hold:{request.HoldId}", ct);

        var holdRepo = _uow.Repository<TicketHold, int>();
        var hold = await holdRepo.GetByIdAsync(request.HoldId, ct)
            ?? throw new NotFoundException(nameof(TicketHold), request.HoldId);

        if (hold.UserId != _currentUser.UserId)
            throw new ForbiddenException("Vé giữ chỗ này không thuộc về bạn.");

        // Giữ chỗ đã được PurchaseTicketCommandHandler tiêu (Payment và vé Pending đã tồn tại) thì
        // không được xoá âm thầm: một lần bấm lại của client sau khi mua thành công sẽ xoá mất bản
        // ghi giữ chỗ đứng sau một khoản thanh toán thật.
        if (hold.IsReleased)
            throw new ConflictException("Vé giữ chỗ này đã được dùng để mua vé, không thể huỷ.");

        holdRepo.Remove(hold);
        await _uow.SaveChangesAsync(ct);

        return true;
    }
}
