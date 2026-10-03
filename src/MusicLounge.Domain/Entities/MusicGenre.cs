namespace MusicLounge.Domain.Entities;

// AuditableEntity (CreatedAt/UpdatedAt/CreatedBy/UpdatedBy) — cùng lý do với EventCategory.
public sealed class MusicGenre : Common.AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }

    // MLACP-581: ảnh riêng do Admin đặt cho thẻ thể loại ở trang chủ. Không bắt buộc — null thì giao diện mượn ảnh của
    // một buổi hòa nhạc thuộc thể loại này (poster, rồi ảnh phòng trà), như trước khi có cột này.
    public string? ImageUrl { get; set; }

    public ICollection<LoungeShowGenre> LoungeShowGenres { get; set; } = [];
    public ICollection<PerformerGenre> PerformerGenres { get; set; } = [];
    public ICollection<UserFavouriteGenre> UserFavourites { get; set; } = [];
}
