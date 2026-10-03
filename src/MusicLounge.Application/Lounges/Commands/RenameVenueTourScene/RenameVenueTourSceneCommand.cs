using FluentValidation;
using MediatR;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.RenameVenueTourScene;

/// <summary>
/// MLACP-586. Đổi tên một cảnh của tour 360°. Trước đây tên chỉ nhận được LÚC THÊM cảnh; cảnh đã có thì chỉ xoá hoặc đặt
/// vị trí — muốn sửa tên phải xoá rồi thêm lại, mất hết điểm bấm (hotspot) đã đặt. Chủ dự án 03/10/2026 thấy ghim
/// "Cảnh 1" trên trang phòng trà: "không đổi tên được sao".
///
/// Tên rỗng / toàn khoảng trắng = bỏ tên (giao diện lại gọi là "Cảnh N").
/// </summary>
public sealed record RenameVenueTourSceneCommand(Guid LoungeId, Guid SceneId, string? Name) : ICommand;

public sealed class RenameVenueTourSceneCommandValidator : AbstractValidator<RenameVenueTourSceneCommand>
{
    public RenameVenueTourSceneCommandValidator()
    {
        RuleFor(x => x.LoungeId).NotEmpty();
        RuleFor(x => x.SceneId).NotEmpty();
        // Cùng giới hạn với lúc thêm cảnh (AddVenueTourSceneCommandValidator).
        RuleFor(x => x.Name).MaximumLength(100).WithMessage("Tên cảnh không dài quá 100 ký tự.");
    }
}

internal sealed class RenameVenueTourSceneCommandHandler : IRequestHandler<RenameVenueTourSceneCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public RenameVenueTourSceneCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RenameVenueTourSceneCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        var sceneRepo = _uow.Repository<VenueTourScene, Guid>();
        var scene = await sceneRepo.GetByIdAsync(request.SceneId, ct);
        if (scene is null || scene.LoungeId != request.LoungeId)
            throw new NotFoundException(nameof(VenueTourScene), request.SceneId);

        scene.Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        sceneRepo.Update(scene);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
