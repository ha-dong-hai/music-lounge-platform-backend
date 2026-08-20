using MediatR;

namespace MusicLounge.Application.Users.Commands.VerifyPhone;

public sealed record VerifyPhoneCommand(string Code) : IRequest<Unit>;
