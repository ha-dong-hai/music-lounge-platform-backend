using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Queries.GetCitizenCardImage;

internal sealed class GetCitizenCardImageQueryHandler
    : IRequestHandler<GetCitizenCardImageQuery, CitizenCardImageDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;

    public GetCitizenCardImageQueryHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IFileStorageService fileStorage)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    public async Task<CitizenCardImageDto> Handle(GetCitizenCardImageQuery request, CancellationToken ct)
    {
        // Own citizen card, or Admin reviewing anyone's for identity verification — nobody else.
        if (_currentUser.UserId != request.TargetUserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền xem ảnh CCCD/CMND này.");

        var user = await _uow.Repository<User, int>().GetByIdAsync(request.TargetUserId, ct)
            ?? throw new NotFoundException(nameof(User), request.TargetUserId);

        var privateRef = request.Side.Equals("front", StringComparison.OrdinalIgnoreCase)
            ? user.CitizenCardFrontImageUrl
            : request.Side.Equals("back", StringComparison.OrdinalIgnoreCase)
                ? user.CitizenCardBackImageUrl
                : throw new DomainException("Side phải là 'front' hoặc 'back'.");

        if (string.IsNullOrEmpty(privateRef))
            throw new NotFoundException("CitizenCardImage", request.TargetUserId);

        var (content, contentType) = await _fileStorage.OpenPrivateFileAsync(privateRef, ct);
        return new CitizenCardImageDto(content, contentType);
    }
}
