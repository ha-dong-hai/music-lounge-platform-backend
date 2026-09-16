using MusicLounge.Application.Lounges.DTOs;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record LoungeSummaryDto(
    int Id,
    string Name,
    string Street,
    string Ward,
    string District,
    string City,
    string FullAddress,
    double? Latitude,
    double? Longitude,
    string? PrimaryImageUrl,
    string? Model3DUrl,
    string? AtmosphereName,
    // MLACP-413: dung chung LoungeGalleryImageDto cua Lounges.DTOs. Truoc day day la mot record rieng TRUNG TEN voi no,
    // nen Swashbuckle khong sinh noi tai lieu API (schemaId dung nhau) va /swagger/v1/swagger.json tra 500 tren Azure.
    IReadOnlyList<LoungeGalleryImageDto> GalleryImages);
