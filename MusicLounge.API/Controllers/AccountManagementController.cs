using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Application.DTOs.AccountManagement;
using MusicLounge.Application.Interfaces;

namespace MusicLounge.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class AccountManagementController : ControllerBase
{
    private readonly IS_AccountManagement _s_AccountManagement;

    public AccountManagementController(IS_AccountManagement s_AccountManagement)
    {
        _s_AccountManagement = s_AccountManagement;
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] MReq_AccountManagementCreate request)
    {
        var res = await _s_AccountManagement.Create(request);
        return Ok(res);
    }

    [HttpGet("get-all")]
    public async Task<IActionResult> GetAll([FromQuery] MReq_AccountManagementGetAll request)
    {
        var res = await _s_AccountManagement.GetAll(request);
        return Ok(res);
    }

    [HttpGet("detail/{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var res = await _s_AccountManagement.GetDetail(id);
        return Ok(res);
    }

    [HttpPut("update")]
    public async Task<IActionResult> Update([FromBody] MReq_AccountManagementUpdate request)
    {
        var res = await _s_AccountManagement.Update(request);
        return Ok(res);
    }

    [HttpDelete("delete/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var res = await _s_AccountManagement.Delete(id);
        return Ok(res);
    }
}
