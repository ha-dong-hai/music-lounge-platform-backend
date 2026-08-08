using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using AppValidationException = MusicLounge.Application.Common.Exceptions.ValidationException;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/uploads")]
public sealed class UploadsController : ControllerBase
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5MB
    private const long MaxModel3DSizeBytes = 30 * 1024 * 1024; // 30MB — file .glb thuc te co the kha lon

    private readonly IFileStorageService _fileStorage;

    public UploadsController(IFileStorageService fileStorage) => _fileStorage = fileStorage;

    /// <summary>Lưu ảnh lên disk cục bộ (wwwroot/uploads), trả về URL tương đối để dùng ngay cho PrimaryImageUrl/CoverImageUrl/avatar/CCCD...</summary>
    [HttpPost("images")]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [RequestSizeLimit(MaxImageSizeBytes)]
    [ProducesResponseType<ApiResponse<UploadImageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new AppValidationException(
                new Dictionary<string, string[]> { ["File"] = ["Vui lòng chọn file ảnh."] });

        if (file.Length > MaxImageSizeBytes)
            throw new AppValidationException(
                new Dictionary<string, string[]> { ["File"] = ["File ảnh không được vượt quá 5MB."] });

        await using var stream = file.OpenReadStream();
        var url = await _fileStorage.SaveImageAsync(stream, file.FileName, ct);

        return Ok(ApiResponse<UploadImageResponse>.Ok(new UploadImageResponse(url)));
    }

    /// <summary>Lưu file mô hình 3D (.glb/.gltf) cho tour ảo phòng trà — Owner tự upload nếu có.</summary>
    [HttpPost("models")]
    [Authorize(Policy = Policies.RequireOwner)]
    [RequestSizeLimit(MaxModel3DSizeBytes)]
    [ProducesResponseType<ApiResponse<UploadImageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadModel3D(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new AppValidationException(
                new Dictionary<string, string[]> { ["File"] = ["Vui lòng chọn file mô hình 3D."] });

        if (file.Length > MaxModel3DSizeBytes)
            throw new AppValidationException(
                new Dictionary<string, string[]> { ["File"] = ["File mô hình 3D không được vượt quá 30MB."] });

        await using var stream = file.OpenReadStream();
        var url = await _fileStorage.SaveModel3DAsync(stream, file.FileName, ct);

        return Ok(ApiResponse<UploadImageResponse>.Ok(new UploadImageResponse(url)));
    }
}

public sealed record UploadImageResponse(string Url);
