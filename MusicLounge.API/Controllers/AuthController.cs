using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.API.Extensions;
using MusicLounge.Application.DTOs.Auth;
using MusicLounge.Application.Interfaces;

namespace MusicLounge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IS_Auth _s_Auth;

    public AuthController(IS_Auth s_Auth)
    {
        _s_Auth = s_Auth;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] MReq_Register request)
    {
        var res = await _s_Auth.Register(request);
        return Ok(res);
    }

    [AllowAnonymous]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] MReq_VerifyEmail request)
    {
        var res = await _s_Auth.VerifyEmail(request);
        return Ok(res);
    }

    [AllowAnonymous]
    [HttpPost("resend-verification-code")]
    public async Task<IActionResult> ResendVerificationCode([FromBody] MReq_ResendVerificationCode request)
    {
        var res = await _s_Auth.ResendVerificationCode(request);
        return Ok(res);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] MReq_Login request)
    {
        var res = await _s_Auth.Login(request);
        return Ok(res);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var res = await _s_Auth.GetProfile(User.GetUserId());
        return Ok(res);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] MReq_UpdateProfile request)
    {
        var res = await _s_Auth.UpdateProfile(User.GetUserId(), request);
        return Ok(res);
    }

    [Authorize]
    [HttpPut("citizen-card")]
    public async Task<IActionResult> UpdateCitizenCard([FromBody] MReq_UpdateCitizenCard request)
    {
        var res = await _s_Auth.UpdateCitizenCard(User.GetUserId(), request);
        return Ok(res);
    }

    [Authorize]
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount()
    {
        var res = await _s_Auth.DeleteAccount(User.GetUserId());
        return Ok(res);
    }
}
