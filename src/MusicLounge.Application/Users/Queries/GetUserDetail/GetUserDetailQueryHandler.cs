using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Queries.GetUserDetail;

internal sealed class GetUserDetailQueryHandler : IRequestHandler<GetUserDetailQuery, UserAdminDto>
{
    private readonly IUnitOfWork _uow;

    public GetUserDetailQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<UserAdminDto> Handle(GetUserDetailQuery request, CancellationToken ct)
    {
        var user = await _uow.Repository<User, int>().GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        return new UserAdminDto(
            user.Id, user.Email, user.FullName, user.Phone, user.AvatarUrl,
            user.Role.ToString(), user.IsActive, user.EmailVerifiedAt is not null, user.CreatedAt);
    }
}
