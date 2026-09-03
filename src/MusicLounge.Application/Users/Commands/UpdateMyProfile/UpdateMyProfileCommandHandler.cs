using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateMyProfileCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateMyProfileCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        user.FullName = request.FullName;
        // Changing the phone number invalidates any prior verification of the OLD number — without
        // this, PhoneVerified could keep claiming NĐ 147/2024 verification for a number the user
        // never actually proved they control.
        if (request.Phone != user.Phone)
        {
            user.PhoneVerified = false;
            user.PhoneVerificationCodeHash = null;
            user.PhoneVerificationCodeExpiresAt = null;
        }
        user.Phone = request.Phone;
        user.AvatarUrl = request.AvatarUrl;

        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
