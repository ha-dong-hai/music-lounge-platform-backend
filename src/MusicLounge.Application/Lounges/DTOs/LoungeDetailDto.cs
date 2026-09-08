namespace MusicLounge.Application.Lounges.DTOs;

public sealed record LoungeDetailDto(
    int Id,
    string Name,
    string? PrimaryImageUrl,
    string? Model3DUrl,
    string? AreaLayoutImageUrl,
    string Street,
    string Ward,
    string District,
    string City,
    string FullAddress,
    double? Latitude,
    double? Longitude,
    int FollowerCount,
    int UpcomingShowCount,
    bool? IsFollowing,
    string? Description,
    string? AtmosphereName,
    IReadOnlyList<LoungeGalleryImageDto> GalleryImages,
    // MLACP-307. Them vao cuoi nen khong xe dich truong nao dang co. OwnerId de FE biet nguoi dang
    // xem co phai chu phong tra khong; Status de chinh chu thay duoc ho so cua minh dang cho duyet
    // hay da bi tu choi — truoc day khong co duong nao bao ho dieu do.
    int OwnerId,
    string Status);

public sealed record LoungeGalleryImageDto(int Id, string ImageUrl, string? Caption, int OrderIndex);
