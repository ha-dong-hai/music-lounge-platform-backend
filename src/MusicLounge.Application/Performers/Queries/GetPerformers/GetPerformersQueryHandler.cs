using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Performers.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Performers.Queries.GetPerformers;

// §6.12: READ/ASSIGN — the shared-catalog autocomplete every Owner searches before deciding
// whether to reuse an existing Performer or create a new one.
internal sealed class GetPerformersQueryHandler
    : IRequestHandler<GetPerformersQuery, PaginatedResult<PerformerDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetPerformersQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<PerformerDto>> Handle(GetPerformersQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);
        var search = request.Search?.Trim();
        // MLACP-501. Tài khoản nhận tiền chỉ người TẠO nghệ sĩ mới quản lý được (BankAccountAccess) — trang đó cần đúng
        // nghệ sĩ của mình, mà danh mục là dùng chung: trước đây FE lật tối đa 10 trang × 50 hồ sơ để tự lọc, quá 500 hồ
        // sơ là thiếu. Không truyền thì vẫn là danh mục chung như cũ. Route đã buộc đăng nhập (RequireOwner) nên
        // createdByMe không bao giờ tới đây khi chưa đăng nhập — gọi ẩn danh nhận 401 ở cửa.
        Guid? createdBy = request.CreatedByMe ? _currentUser.UserId : null;

        var (performers, total) = await _uow.Repository<Performer, Guid>().GetPagedAsync(
            p => (string.IsNullOrEmpty(search) || p.Name.Contains(search))
                 && (createdBy == null || p.CreatedByUserId == createdBy),
            p => p.Id,
            page, size, ct);

        var dtos = await PerformerDtoMapper.MapAsync(_uow, performers, ct);
        return new PaginatedResult<PerformerDto>(dtos, page, size, total);
    }
}
