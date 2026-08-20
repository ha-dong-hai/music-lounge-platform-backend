using MediatR;
using MusicLounge.Application.Auth;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.SubmitCitizenCard;

internal sealed class SubmitCitizenCardCommandHandler : IRequestHandler<SubmitCitizenCardCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;
    private readonly IPiiEncryptionService _piiEncryption;

    public SubmitCitizenCardCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IFileStorageService fileStorage,
        IPiiEncryptionService piiEncryption)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _piiEncryption = piiEncryption;
    }

    public async Task<Unit> Handle(SubmitCitizenCardCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();

        // CitizenCardNumber is encrypted at rest (non-deterministic), so uniqueness/lookup has to
        // go through this deterministic hash instead of comparing the encrypted column directly.
        var cardNumberHash = PasswordResetTokenHasher.Hash(request.CitizenCardNumber);

        var takenByOther = await userRepo.AnyAsync(
            u => u.CitizenCardNumberHash == cardNumberHash && u.Id != _currentUser.UserId, ct);
        if (takenByOther)
            throw new ConflictException("Số CCCD/CMND này đã được đăng ký bởi tài khoản khác.");

        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        // Client uploads through the same generic POST /uploads/images endpoint as any public
        // image (lounge photo, avatar...), landing in the publicly-served wwwroot/uploads tree —
        // the instant it's claimed here as a citizen-card image, move it out to a private location
        // that's only reachable via the authenticated GetMyCitizenCardImage/Admin review endpoint,
        // never by guessing/leaking the original static-file URL.
        user.CitizenCardNumber = _piiEncryption.Encrypt(request.CitizenCardNumber);
        user.CitizenCardNumberHash = cardNumberHash;
        user.CitizenCardFrontImageUrl = await _fileStorage.RelocateToPrivateAsync(request.FrontImageUrl, ct);
        user.CitizenCardBackImageUrl = await _fileStorage.RelocateToPrivateAsync(request.BackImageUrl, ct);
        user.CitizenCardSubmittedAt = DateTimeOffset.UtcNow;

        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
