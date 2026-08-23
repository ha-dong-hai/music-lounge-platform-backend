using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Api.Validators;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Api.Controllers;

// Luu y: endpoint /uploads/models (3D .glb cho tour ao) thuoc pham vi task khac, chua dua vao day.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/uploads")]
public sealed class UploadsController : ControllerBase
{
    private readonly IFileStorageService _fileStorage;

    public UploadsController(IFileStorageService fileStorage) => _fileStorage = fileStorage;

    /// <summary>Lưu ảnh lên disk cục bộ (wwwroot/uploads), trả về URL tương đối để dùng ngay cho PrimaryImageUrl/gallery...</summary>
    [HttpPost("images")]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [RequestSizeLimit(UploadImageValidator.MaxSizeBytes)]
    [ProducesResponseType<ApiResponse<UploadImageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct = default)
    {
        await new UploadImageValidator().ValidateAndThrowAppExceptionAsync(file, ct);

        await using var stream = file.OpenReadStream();
        var url = await _fileStorage.SaveImageAsync(stream, file.FileName, ct);

        return Ok(ApiResponse<UploadImageResponse>.Ok(new UploadImageResponse(url)));
    }
}

public sealed record UploadImageResponse(string Url);
