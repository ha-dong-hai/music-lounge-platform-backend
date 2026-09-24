namespace MusicLounge.Application.Lounges.DTOs;

/// <summary>
/// MLACP-485: KHÔNG thêm lại BusinessLicenseUrl vào đây.
///
/// <para>Trường này từng nằm trong record, và <c>GET /lounges</c> là <c>[AllowAnonymous]</c>
/// (<c>LoungesController.cs:56-57</c>) — nên đường dẫn giấy phép kinh doanh của mọi phòng trà được
/// phát ra cho bất kỳ ai gọi, không cần đăng nhập. Đo thật 23/09/2026: gọi <c>GET /lounges</c>
/// không kèm token nào và nhận về <c>"private-uploads/f59b17bb…jpg"</c>.</para>
///
/// <para>Trước đó việc để nó ở đây được coi là vô hại, dựa trên lập luận "file đã chuyển sang vùng
/// riêng tư nên URL trong DTO không tải trực tiếp được". Lập luận đó SAI: <c>objects.copy</c> của
/// GCS kế thừa custom metadata, nên <c>firebaseStorageDownloadTokens</c> đi theo sang bản riêng tư
/// và file vẫn tải được bằng token cũ (DEF-BE-04, đo được HTTP 200 + 927KB).</para>
///
/// <para>Lý do thứ hai để giữ nó — "đổi cấu trúc response là thứ phía client đang dùng không được
/// phép hứng chịu" — cũng không đứng: đã grep cả <c>mlacp-ui</c> lẫn hai app Flutter, KHÔNG nơi nào
/// đọc <c>businessLicenseUrl</c>. Đường xem hợp lệ duy nhất là
/// <c>GET /lounges/{id}/business-license</c>, vốn có kiểm quyền.</para>
/// </summary>
public sealed record LoungeListItemDto(
    int Id,
    string Name,
    string? PrimaryImageUrl,
    string? Model3DUrl,
    string? AreaLayoutImageUrl,
    string Street,
    string District,
    string City,
    int FollowerCount,
    int UpcomingShowCount);
