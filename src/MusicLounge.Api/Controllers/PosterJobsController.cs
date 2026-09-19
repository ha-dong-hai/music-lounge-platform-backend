using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Filters;
using MusicLounge.Api.Validators;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.PosterJobs.Commands.ClaimPosterJob;
using MusicLounge.Application.PosterJobs.Commands.CompletePosterJob;
using MusicLounge.Application.PosterJobs.Commands.FailPosterJob;
using MusicLounge.Application.PosterJobs.DTOs;

namespace MusicLounge.Api.Controllers;

/// <summary>
/// MLACP-458. Ba endpoint dành RIÊNG cho máy trạm sinh poster qua Google Flow — không phải cho người dùng.
///
/// Google Flow chỉ nhận lệnh phát ra từ một tab trình duyệt đã đăng nhập, nên máy chủ trên Azure không gọi thẳng được:
/// chiều gọi phải đảo lại, máy trạm tự đến nhận việc. Xác thực bằng khoá riêng, xem <see cref="PosterWorkerKeyAttribute"/>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/poster-jobs")]
[PosterWorkerKey]
public sealed class PosterJobsController : ControllerBase
{
    private readonly ISender _sender;

    public PosterJobsController(ISender sender) => _sender = sender;

    /// <summary>Máy trạm xin đơn cũ nhất đang chờ. 204 khi hàng đợi rỗng — đây là câu trả lời thường gặp nhất.</summary>
    [HttpPost("claim")]
    [ProducesResponseType<ApiResponse<PosterJobDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Claim([FromBody] ClaimPosterJobRequest body, CancellationToken ct = default)
    {
        var job = await _sender.Send(new ClaimPosterJobCommand(TenMayTram(body?.WorkerId)), ct);
        return job is null ? NoContent() : Ok(ApiResponse<PosterJobDto>.Ok(job));
    }

    /// <summary>Máy trạm nộp ảnh đã sinh xong. Ảnh được kiểm theo chữ ký file, không tin phần mở rộng.</summary>
    [HttpPost("{id:int}/result")]
    [RequestSizeLimit(UploadImageValidator.MaxSizeBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Result(
        int id, IFormFile file, [FromForm] string? workerId, CancellationToken ct = default)
    {
        await new UploadImageValidator().ValidateAndThrowAppExceptionAsync(file, ct);

        // Đọc hết vào bộ nhớ: ảnh tối đa 5MB theo UploadImageValidator, và handler cần chính nội dung đó để nhận dạng
        // định dạng thật trước khi đặt tên file (MLACP-421).
        using var buffer = new MemoryStream();
        await using (var stream = file.OpenReadStream())
        {
            await stream.CopyToAsync(buffer, ct);
        }

        await _sender.Send(new CompletePosterJobCommand(id, TenMayTram(workerId), buffer.ToArray()), ct);
        return NoContent();
    }

    /// <summary>Máy trạm báo không sinh được ảnh (Google Flow từ chối, hết hạn mức, mất mạng...).</summary>
    [HttpPost("{id:int}/fail")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Fail(
        int id, [FromBody] FailPosterJobRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new FailPosterJobCommand(id, TenMayTram(body?.WorkerId), body?.Reason ?? "Không rõ lý do."), ct);
        return NoContent();
    }

    /// <summary>Tên máy trạm chỉ để ghi nhật ký; thiếu thì vẫn chạy được, nhưng phải có một giá trị để đối chiếu khi nộp bài.</summary>
    private static string TenMayTram(string? workerId)
        => string.IsNullOrWhiteSpace(workerId) ? "unknown-worker" : workerId.Trim()[..Math.Min(workerId.Trim().Length, 60)];
}

public sealed record ClaimPosterJobRequest(string? WorkerId);

public sealed record FailPosterJobRequest(string? WorkerId, string? Reason);
