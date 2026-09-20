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
    IReadOnlyList<int> FavouriteAtmosphereIds,
    // PUT /me/preferences ghi đè toàn phần và nhận cả DislikedGenreIds, nhưng trước đây không đường
    // đọc nào trả danh sách loại trừ về. Người dùng mở lại trang sở thích rồi bấm Lưu là mất sạch
    // phần đã loại trừ — mà loại trừ thể loại chính là cách duy nhất họ nói "đừng gợi ý thứ này nữa".
    IReadOnlyList<int> DislikedGenreIds);
