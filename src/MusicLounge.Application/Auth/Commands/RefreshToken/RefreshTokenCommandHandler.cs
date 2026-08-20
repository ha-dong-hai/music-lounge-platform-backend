using System.Security.Claims;
using MediatR;
using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Auth.Commands.RefreshToken;

internal sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwtTokenService;

    public RefreshTokenCommandHandler(IUnitOfWork uow, IJwtTokenService jwtTokenService)
    {
        _uow = uow;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var principal = _jwtTokenService.ValidateRefreshToken(request.RefreshToken);
        if (principal is null)
            throw new UnauthorizedException("Refresh token không hợp lệ hoặc đã hết hạn.");

        if (!int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ||
            !Guid.TryParse(principal.FindFirst("sec_stamp")?.Value, out var tokenStamp))
            throw new UnauthorizedException("Refresh token không hợp lệ hoặc đã hết hạn.");

        var user = await _uow.Repository<User, int>().GetByIdAsync(userId, ct);

        // Same comparison as ActiveUserBehavior: a refresh token issued before logout carries the
        // OLD SecurityStamp, so this catches a revoked session even though the JWT itself is still
        // cryptographically valid — this is the actual revocation mechanism, not just an active-user
        // check.
        if (user is null || !user.IsActive || user.SecurityStamp != tokenStamp)
            throw new UnauthorizedException("Refresh token không hợp lệ hoặc đã hết hạn.");

        int? loungeId = null;
        if (user.Role == UserRole.Staff)
        {
            var staffAssignments = await _uow.Repository<LoungeStaff, int>()
                .FindAsync(s => s.UserId == user.Id && s.IsActive, ct);
            loungeId = staffAssignments.FirstOrDefault()?.LoungeId;
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user, loungeId);
        // Rotate the refresh token too (not just reuse the one the client sent) — a fresh token per
        // refresh limits how long a stolen-but-unused refresh token stays exploitable.
        var (newRefreshToken, refreshExpiresAt) = _jwtTokenService.GenerateRefreshToken(user);

        return new AuthResultDto(
            token, expiresAt, user.Id, user.Email, user.FullName, user.Role.ToString(), loungeId,
            newRefreshToken, refreshExpiresAt);
    }
}
