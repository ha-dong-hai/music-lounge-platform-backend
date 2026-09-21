using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using CustomCriteriaEntity = MusicLounge.Domain.Entities.CustomCriteria;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.CustomCriteria.Commands.UpdateCustomCriteria;

internal sealed class UpdateCustomCriteriaCommandHandler : IRequestHandler<UpdateCustomCriteriaCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateCustomCriteriaCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateCustomCriteriaCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<CustomCriteriaEntity, int>();
        var criteria = await repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(CustomCriteriaEntity), request.Id);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(criteria.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), criteria.LoungeId);

        // Cùng luật quyền với lệnh tạo và lệnh gắn giá trị: tiêu chí riêng là cách phòng trà tự phân loại
        // buổi diễn của mình.
        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền sửa tiêu chí của venue này.");

        criteria.Name = request.Name;

        // Tắt KHÔNG xoá giá trị đã gắn cho các buổi diễn: lịch sử giữ nguyên, chỉ là không đề xuất tiêu
        // chí này khi dựng màn hình mới nữa. Đường đọc giá trị vẫn trả về kèm cờ để màn hình biết mà hiển
        // thị cho đúng.
        criteria.IsActive = request.IsActive;

        repo.Update(criteria);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
