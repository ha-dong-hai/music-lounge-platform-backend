using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MusicLounge.Application.Auth.Commands.ForgotPassword;
using MusicLounge.Application.Auth.Commands.GoogleLogin;
using MusicLounge.Application.Auth.Commands.Login;
using MusicLounge.Application.Auth.Commands.Register;
using MusicLounge.Application.Auth.Commands.ResendVerificationCode;
using MusicLounge.Application.Auth.Commands.ResetPassword;
using MusicLounge.Application.Auth.Commands.VerifyEmail;
using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[EnableRateLimiting("auth")]
// Every action here is intentionally public (you can't authenticate to an endpoint that issues
// authentication). Previously relied on no global fallback authorization policy existing yet to
// stay reachable — safe today, but silent: if a stricter fallback is ever added elsewhere, this
// whole controller would start requiring a token to reach the endpoints that issue one, with no
// visible cause. Explicit here so that risk can't reappear invisibly.
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender) => _sender = sender;

    /// <summary>Chỉ tạo tài khoản "chưa xác thực" và gửi mã OTP qua email — không cấp token. Gọi
    /// verify-email với mã đúng thì mới nhận token đăng nhập lần đầu.</summary>
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

    /// <summary>Luôn trả cùng shape response bất kể email có tồn tại hay đã xác thực hay không — tránh lộ thông tin tài khoản.</summary>
    [HttpPost("resend-verification-code")]
    [ProducesResponseType<ApiResponse<ResendVerificationCodeResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendVerificationCode(
        [FromBody] ResendVerificationCodeCommand command, CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return Ok(ApiResponse<ResendVerificationCodeResultDto>.Ok(result));
    }

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

    /// <summary>Luôn trả 204 bất kể email có tồn tại hay không — tránh lộ email nào đã đăng ký.</summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordCommand command, CancellationToken ct = default)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordCommand command, CancellationToken ct = default)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }
}

public sealed record LoginRequest(string Email, string Password);
