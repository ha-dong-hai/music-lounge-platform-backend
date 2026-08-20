using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Api.Validators;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/uploads")]
public sealed class UploadsController : ControllerBase
{
    private readonly IFileStorageService _fileStorage;

    public UploadsController(IFileStorageService fileStorage) => _fileStorage = fileStorage;

    /// <summary>Lưu ảnh lên disk cục bộ (wwwroot/uploads), trả về URL tương đối để dùng ngay cho PrimaryImageUrl/CoverImageUrl/avatar/CCCD...</summary>
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

    /// <summary>Lưu file mô hình 3D (.glb/.gltf) cho tour ảo phòng trà — Owner tự upload nếu có.</summary>
    [HttpPost("models")]
    [Authorize(Policy = Policies.RequireOwner)]
    [RequestSizeLimit(UploadModel3DValidator.MaxSizeBytes)]
    [ProducesResponseType<ApiResponse<UploadImageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadModel3D(IFormFile file, CancellationToken ct = default)
    {
        await new UploadModel3DValidator().ValidateAndThrowAppExceptionAsync(file, ct);

        await using var stream = file.OpenReadStream();
        var url = await _fileStorage.SaveModel3DAsync(stream, file.FileName, ct);

        return Ok(ApiResponse<UploadImageResponse>.Ok(new UploadImageResponse(url)));
    }
}

public sealed record UploadImageResponse(string Url);
