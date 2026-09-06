namespace MusicLounge.Domain.Entities;

// AuditableEntity (CreatedAt/UpdatedAt/CreatedBy/UpdatedBy, auto-stamped by
// ApplicationDbContext.SaveChangesAsync) — ai tạo/sửa 1 danh mục cần biết được, dù là dữ liệu
// tham chiếu ít nhạy cảm hơn các entity chạm tiền thật.
public sealed class EventCategory : Common.AuditableEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<LoungeShow> Shows { get; set; } = [];
}
