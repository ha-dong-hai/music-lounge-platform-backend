using MusicLounge.Application.Common;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Settlements;

/// <summary>
/// MLACP-399. Tên mà chủ tài khoản nhận tiền của phòng trà phải khớp.
///
/// <para>Hộ/cá nhân: họ tên được chốt lúc Admin duyệt CCCD/CMND (<see cref="User.CitizenCardVerifiedName"/>) — không phải
/// FullName hiện tại, vì FullName sửa được bất cứ lúc nào mà không mất trạng thái đã duyệt. Doanh nghiệp đã được duyệt hồ sơ
/// thuế: tên doanh nghiệp (MLACP-398). Doanh nghiệp chưa được duyệt xử lý như hộ/cá nhân, cùng quy tắc khấu trừ thuế.</para>
///
/// <para>Sandbox không tra được tên chủ tài khoản qua ngân hàng, nên đây là phép so với tên chủ phòng trà tự nhập: nó chặn
/// việc xác minh một tài khoản khai là đứng tên người khác, không chứng minh tên nhập vào đúng với ngân hàng.</para>
/// </summary>
public static class PayoutAccountName
{
    public sealed record Expected(string? Name, bool IsEnterprise);

    public static Expected ExpectedFor(User owner)
    {
        var isEnterprise = owner.BusinessType == PayeeBusinessType.Enterprise && owner.TaxProfileVerifiedAt is not null;
        return new Expected(isEnterprise ? owner.LegalName : owner.CitizenCardVerifiedName, isEnterprise);
    }

    public static bool Matches(string accountHolder, string expectedName)
        => VietnameseText.Fold(accountHolder) == VietnameseText.Fold(expectedName);
}
