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

    /// <summary>Lưu file mô hình 3D (.glb/.gltf) cho không gian phòng trà, rồi truyền URL trả về
    /// sang PUT /lounges/{id}/model-3d. UploadModel3DValidator (giới hạn 30MB) và
    /// IFileStorageService.SaveModel3DAsync đều đã được viết đầy đủ từ trước và chưa từng có ai
    /// gọi — đây là cái vòi còn thiếu của đường ống đó.</summary>
    [HttpPost("models")]
    [Authorize(Policy = Policies.RequireOwner)]
    [RequestSizeLimit(UploadModel3DValidator.MaxSizeBytes)]
    [ProducesResponseType<ApiResponse<UploadImageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadModel3D(IFormFile file, CancellationToken ct = default)
    {
        await new UploadModel3DValidator().ValidateAndThrowAppExceptionAsync(file, ct);

        await using var stream = file.OpenReadStream();
        var url = await _fileStorage.SaveModel3DAsync(stream, file.FileName, ct);

        return Ok(ApiResponse<UploadImageResponse>.Ok(new UploadImageResponse(url)));
    }
}

public sealed record UploadImageResponse(string Url);
