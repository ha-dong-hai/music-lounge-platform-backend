using MediatR;

namespace MusicLounge.Application.Users.Commands.RequestPhoneVerification;

public sealed record RequestPhoneVerificationCommand : IRequest<Unit>;
