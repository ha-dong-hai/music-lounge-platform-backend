using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Staffing.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Staffing.Queries.FindUserByEmail;

internal sealed class FindUserByEmailQueryHandler : IRequestHandler<FindUserByEmailQuery, UserLookupDto>
{
    private readonly IUnitOfWork _uow;

    public FindUserByEmailQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<UserLookupDto> Handle(FindUserByEmailQuery request, CancellationToken ct)
    {
        var users = await _uow.Repository<User, int>()
            .FindAsync(u => u.Email == request.Email, ct);
        var user = users.FirstOrDefault()
            ?? throw new NotFoundException(nameof(User), request.Email);

        return new UserLookupDto(user.Id, user.FullName, user.Email);
    }
}
