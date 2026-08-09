using MediatR;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<GetCitizenCardImageQueryHandler> _logger;

    public GetCitizenCardImageQueryHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IFileStorageService fileStorage,
        ILogger<GetCitizenCardImageQueryHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _logger = logger;
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

        // Legally sensitive PII (BVDLCN 2025) with no forensic trail otherwise — the DB row doesn't
        // record who viewed it, unlike VenuePenalty (IssuedBy) or account deactivation (this
        // session's earlier LogWarning additions). Only log Admin access to someone ELSE's card —
        // a user opening their own card via the "own" branch above isn't a security-relevant event.
        if (_currentUser.UserId != request.TargetUserId)
            _logger.LogWarning(
                "Admin viewed citizen card image: TargetUserId={TargetUserId} Side={Side} by AdminUserId={AdminUserId} at {At}",
                request.TargetUserId, request.Side, _currentUser.UserId, DateTimeOffset.UtcNow);

        return new CitizenCardImageDto(content, contentType);
    }
}
