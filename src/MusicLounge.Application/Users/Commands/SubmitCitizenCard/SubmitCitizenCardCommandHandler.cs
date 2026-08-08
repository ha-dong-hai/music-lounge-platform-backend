using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.SubmitCitizenCard;

internal sealed class SubmitCitizenCardCommandHandler : IRequestHandler<SubmitCitizenCardCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SubmitCitizenCardCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SubmitCitizenCardCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();

        var takenByOther = await userRepo.AnyAsync(
            u => u.CitizenCardNumber == request.CitizenCardNumber && u.Id != _currentUser.UserId, ct);
        if (takenByOther)
            throw new ConflictException("Số CCCD/CMND này đã được đăng ký bởi tài khoản khác.");

        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        user.CitizenCardNumber = request.CitizenCardNumber;
        user.CitizenCardFrontImageUrl = request.FrontImageUrl;
        user.CitizenCardBackImageUrl = request.BackImageUrl;
        user.CitizenCardSubmittedAt = DateTimeOffset.UtcNow;

        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
