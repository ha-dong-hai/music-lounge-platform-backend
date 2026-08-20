using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MusicLounge.Application.Auth.Commands.GoogleLogin;
using MusicLounge.Application.Auth.Commands.Login;
using MusicLounge.Application.Auth.Commands.Logout;
using MusicLounge.Application.Auth.Commands.RefreshToken;
using MusicLounge.Application.Auth.Commands.Register;
using MusicLounge.Application.Auth.Commands.ResendVerificationCode;
using MusicLounge.Application.Auth.Commands.VerifyEmail;
using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[EnableRateLimiting("auth")]
// [AllowAnonymous] applied per-action, not at class level — Logout below needs [Authorize] to
// actually require a token, and a class-level [AllowAnonymous] bypasses every [Authorize]
// underneath it regardless of ordering/specificity.
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender) => _sender = sender;

    /// <summary>Chỉ tạo tài khoản "chưa xác thực" và gửi mã OTP qua email — không cấp token. Gọi
    /// verify-email với mã đúng thì mới nhận token đăng nhập lần đầu.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<ApiResponse<RegisterResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command, CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return Ok(ApiResponse<RegisterResultDto>.Ok(result));
    }

    [AllowAnonymous]
    [HttpPost("verify-email")]
    [ProducesResponseType<ApiResponse<AuthResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailCommand command, CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return Ok(ApiResponse<AuthResultDto>.Ok(result));
    }

    /// <summary>Luôn trả 204 bất kể email có tồn tại hay đã xác thực hay không — tránh lộ thông tin tài khoản.</summary>
    [AllowAnonymous]
    [HttpPost("resend-verification-code")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendVerificationCode(
        [FromBody] ResendVerificationCodeCommand command, CancellationToken ct = default)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<ApiResponse<AuthResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest body, CancellationToken ct = default)
    {
        // IpAddress is deliberately NOT part of the request body a client could bind directly —
        // LoginSpikeDetectionJob keys entirely on this value, so a client-supplied one would let an
        // attacker spoof or rotate it to dodge detection. Read from the connection itself, same
        // "best-effort, not proxy-aware" approach SubscribeToPackageCommandHandler already uses for
        // its own IP capture.
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _sender.Send(new LoginCommand(body.Email, body.Password, ip), ct);
        return Ok(ApiResponse<AuthResultDto>.Ok(result));
    }

    [AllowAnonymous]
    [HttpPost("google")]
    [ProducesResponseType<ApiResponse<AuthResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Google(
        [FromBody] GoogleLoginCommand command, CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return Ok(ApiResponse<AuthResultDto>.Ok(result));
    }

    /// <summary>Không cần Bearer access token còn hạn — bản thân refresh token trong body là thứ
    /// chứng minh danh tính, vì access token hết hạn chính là lý do gọi endpoint này.</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<ApiResponse<AuthResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenCommand command, CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return Ok(ApiResponse<AuthResultDto>.Ok(result));
    }

    /// <summary>Yêu cầu access token hợp lệ — xoay SecurityStamp của user hiện tại, vô hiệu hóa mọi
    /// access/refresh token đã cấp trước đó ngay lập tức.</summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct = default)
    {
        await _sender.Send(new LogoutCommand(), ct);
        return NoContent();
    }
}

public sealed record LoginRequest(string Email, string Password);
