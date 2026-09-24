using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.UpdateMyLanguage;

internal sealed class UpdateMyLanguageCommandHandler : IRequestHandler<UpdateMyLanguageCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateMyLanguageCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateMyLanguageCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        // Chỉ ảnh hưởng những gì gửi TỪ GIỜ. Thông báo đã tạo vẫn có đủ hai bản trong CSDL nên danh sách thông báo tự
        // đổi theo Accept-Language; push/email/SMS đã gửi thì không gửi lại.
        user.PreferredLanguage = request.PreferredLanguage;

        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
