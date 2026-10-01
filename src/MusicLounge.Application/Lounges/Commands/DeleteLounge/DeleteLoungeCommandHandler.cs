using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.DeleteLounge;

internal sealed class DeleteLoungeCommandHandler : IRequestHandler<DeleteLoungeCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public DeleteLoungeCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteLoungeCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await repo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền xóa venue này.");

        // DONE WHEN: "Xoa phong tra thanh cong khi khong co su kien" - chan xoa neu con bat ky
        // LoungeShow nao (moi trang thai, khong chi show dang "hoat dong") de tranh mat lich su
        // du lieu cua show da ket thuc/huy.
        //
        // MLACP-507 (M-405 mục 3): câu 409 cũ chỉ nói "đang có buổi diễn", trong khi thứ chặn thường là BẢN NHÁP — loại
        // không hiện ở danh sách công khai — nên người xoá (kể cả Admin) phải dò từng id mới biết vì sao. Giờ trả kèm
        // danh sách buổi đang chặn (mã, tên, trạng thái) ở errors.blockingShows. Người gọi tới được đây đã qua kiểm quyền
        // chủ phòng trà/Admin ở trên, nên không lộ bản nháp cho người ngoài.
        var dangChan = (await _uow.Repository<LoungeShow, int>()
                .FindAsync(s => s.LoungeId == request.LoungeId, ct))
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Name, Status = s.Status.ToString() })
            .ToList();
        if (dangChan.Count > 0)
            throw new ConflictException(
                "Phòng trà vẫn còn buổi diễn (tính cả bản nháp, buổi đã kết thúc hoặc đã huỷ) nên không thể xóa.",
                new { blockingShows = dangChan });

        repo.Remove(lounge);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
