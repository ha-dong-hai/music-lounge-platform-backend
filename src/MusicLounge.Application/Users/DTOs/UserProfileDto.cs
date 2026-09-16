namespace MusicLounge.Application.Users.DTOs;

public sealed record UserProfileDto(
    int Id,
    string FullName,
    string Email,
    // MLACP-427: truoc day khong tra hai truong nay, nen xac minh xong giao dien khong biet de hien "da xac minh", an nut
    // "gui ma", hay yeu cau xac minh lai khi nguoi dung doi so (doi so thi he thong tu huy xac minh — NĐ 147/2024).
    // Du lieu cua chinh nguoi dang dang nhap nen tra du so.
    string? Phone,
    bool PhoneVerified,
    string? AvatarUrl,
    bool AiConsent,
    IReadOnlyList<int> FavouriteGenreIds,
    IReadOnlyList<int> FavouriteMoodIds,
    IReadOnlyList<int> FavouriteAtmosphereIds);
