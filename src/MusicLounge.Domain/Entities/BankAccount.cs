using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// D1: AuditableEntity (CreatedAt/UpdatedAt/CreatedBy/UpdatedBy, auto-stamped by
// ApplicationDbContext.SaveChangesAsync) — this is the payout-destination record for real money
// (settlement/donation), so knowing who added/changed an account number matters more here than on
// most reference-data entities.
public sealed class BankAccount : Common.AuditableEntity<Guid>
{
    public BankAccountOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }    // polymorphic: lounge.id or performer.id — no FK
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = true;
    public bool IsVerified { get; set; } = false;

    // MLACP-668. Admin TỪ CHỐI tài khoản nhận tiền của phòng trà. Trước đây từ chối chỉ ghi IsVerified=false — giá trị
    // tài khoản đang chờ vốn đã có — nên không gì đổi: tài khoản nằm lại hàng chờ của Admin, chủ phòng trà vẫn thấy
    // "Chờ Admin duyệt" và lý do từ chối chỉ nằm trong một thông báo. Có giá trị = đã bị từ chối và CHƯA sửa lại;
    // chủ phòng trà sửa tài khoản (UpdateBankAccount) hoặc Admin xác minh thì xoá về null — quay lại hàng chờ.
    public DateTimeOffset? RejectedAt { get; set; }
    public string? RejectionNote { get; set; }
}
