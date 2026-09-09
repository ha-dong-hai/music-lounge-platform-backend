namespace MusicLounge.Application.Analytics.DTOs;

/// <param name="Model">Tên mô hình được đo.</param>
/// <param name="HitRateAtKPercent">
/// Tỉ lệ phần trăm trường hợp mà buổi diễn bị giấu lọt vào top K.
/// </param>
/// <param name="CatalogueCoveragePercent">
/// Tỉ lệ phần trăm kho buổi diễn từng được mô hình đưa vào top K của ít nhất một người. Thấp nghĩa
/// là mô hình chỉ quanh quẩn vài buổi quen thuộc.
/// </param>
public sealed record ModelEvaluationDto(
    string Model,
    int Cases,
    int Hits,
    decimal HitRateAtKPercent,
    decimal CatalogueCoveragePercent);

/// <param name="Status">
/// 'Evaluated' khi có số. 'NotEnoughHistory' khi chưa đủ người dùng có lịch sử để phép đo có nghĩa —
/// và khi đó KHÔNG có con số nào, vì một con số dựng trên ba người dùng chỉ gây hiểu nhầm.
/// </param>
/// <param name="Caveat">
/// Giới hạn phải đọc kèm mọi con số ở đây. Không phải phần trang trí: một chỉ số đánh giá đưa ra mà
/// không kèm điều kiện áp dụng thì sẽ bị trích dẫn sai.
/// </param>
public sealed record RecommenderEvaluationDto(
    string Status,
    string Method,
    string Caveat,
    int K,
    int UsersWithEnoughHistory,
    int CatalogueSize,
    IReadOnlyList<ModelEvaluationDto> Models);
