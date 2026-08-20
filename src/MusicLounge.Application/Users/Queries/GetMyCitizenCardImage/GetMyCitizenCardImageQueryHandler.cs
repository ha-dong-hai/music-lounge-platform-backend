using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Queries.GetMyCitizenCardImage;

internal sealed class GetMyCitizenCardImageQueryHandler
    : IRequestHandler<GetMyCitizenCardImageQuery, CitizenCardImageDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;

    public GetMyCitizenCardImageQueryHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IFileStorageService fileStorage)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    public async Task<CitizenCardImageDto> Handle(GetMyCitizenCardImageQuery request, CancellationToken ct)
    {
        var user = await _uow.Repository<User, int>().GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        var privateRef = request.Side.Equals("front", StringComparison.OrdinalIgnoreCase)
            ? user.CitizenCardFrontImageUrl
            : request.Side.Equals("back", StringComparison.OrdinalIgnoreCase)
                ? user.CitizenCardBackImageUrl
                : throw new DomainException("Side phải là 'front' hoặc 'back'.");

        if (string.IsNullOrEmpty(privateRef))
            throw new NotFoundException("CitizenCardImage", _currentUser.UserId);

        var (content, contentType) = await _fileStorage.OpenPrivateFileAsync(privateRef, ct);
        return new CitizenCardImageDto(content, contentType);
    }
}
