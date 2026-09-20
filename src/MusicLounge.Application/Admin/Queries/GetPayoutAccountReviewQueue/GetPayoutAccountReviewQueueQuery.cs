using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Application.Admin.Queries.GetPayoutAccountReviewQueue;

/// <summary>
/// Hàng đợi tài khoản nhận tiền chờ Admin xác minh.
///
/// <para>Trước task này hệ thống có <c>POST /admin/bank-accounts/{id}/review</c> để duyệt, nhưng không có
/// đường nào LIỆT KÊ để biết {id} là gì. Nghĩa là thao tác duyệt chỉ gọi được nếu Admin đã biết sẵn số id
/// từ nơi khác — trên giao diện thì không có nơi nào như thế, nên màn hình xác minh tài khoản ngân hàng
/// không dựng được, và tiền quyết toán không chuyển đi được cho tới khi ai đó tra tay trong cơ sở dữ liệu.</para>
///
/// <para>Chỉ tài khoản của PHÒNG TRÀ vào hàng đợi này: tài khoản của nghệ sĩ do chính nghệ sĩ xác nhận qua
/// liên kết gửi e-mail, và lệnh duyệt cũng từ chối chúng — liệt kê ra đây thì Admin bấm vào chỉ nhận lỗi.</para>
/// </summary>
/// <param name="Verified">
/// <c>false</c> (mặc định) là phần việc đang chờ. <c>true</c> để tra lại những tài khoản đã xác minh — cần khi
/// có khiếu nại về một khoản chi trả.
/// </param>
public sealed record GetPayoutAccountReviewQueueQuery(
    bool Verified = false,
    int Page = 1,
    int PageSize = 20) : IQuery<PaginatedResult<PayoutAccountReviewItemDto>>;

/// <param name="AccountNumberMasked">
/// Chỉ bốn số cuối. Quyết định xác minh dựa vào TÊN chủ tài khoản đối chiếu với hồ sơ định danh, không dựa
/// vào số tài khoản; hiển thị đủ số chỉ khiến mọi trình duyệt của mọi người duyệt đều giữ lại số tài khoản
/// đầy đủ của chủ phòng trà.
/// </param>
/// <param name="AccountNumberUnreadable">
/// Số tài khoản được mã hoá bằng khoá đã mất. Lệnh duyệt từ chối trường hợp này (MLACP-401), nên báo ngay
/// trên danh sách thay vì để Admin bấm duyệt rồi nhận lỗi.
/// </param>
/// <param name="ExpectedAccountHolder">
/// Tên mà tài khoản PHẢI đứng: họ tên trên CCCD của chủ phòng trà, hoặc tên doanh nghiệp nếu khai hồ sơ
/// doanh nghiệp. Lệnh duyệt đối chiếu đúng tên này (MLACP-399) và từ chối khi lệch; đưa lên danh sách để
/// người duyệt thấy trước, không phải đoán.
/// </param>
/// <param name="HolderNameMatches">
/// Kết quả đối chiếu của chính hàm mà lệnh duyệt dùng. <c>false</c> nghĩa là bấm duyệt sẽ bị từ chối.
/// </param>
/// <param name="OwnerIdentityApproved">
/// Chủ phòng trà đã được duyệt CCCD chưa. Chưa duyệt thì lệnh duyệt tài khoản cũng từ chối, vì không có gì
/// để đối chiếu tên.
/// </param>
public sealed record PayoutAccountReviewItemDto(
    int Id,
    int LoungeId,
    string LoungeName,
    int OwnerUserId,
    string OwnerName,
    string BankName,
    string AccountNumberMasked,
    bool AccountNumberUnreadable,
    string AccountHolder,
    string? ExpectedAccountHolder,
    bool HolderNameMatches,
    bool OwnerIdentityApproved,
    bool IsDefault,
    bool IsVerified,
    DateTime CreatedAt);
