namespace MusicLounge.Domain.Entities;

/// <summary>
/// Thể loại người dùng nói thẳng là không thích.
///
/// Đối xứng với <see cref="UserFavouriteGenre"/>: cùng hình dạng, ngược dấu.
///
/// <b>Vì sao phải là một hành động chủ động, không suy ra được.</b> Sở thích tiêu cực không nằm
/// trong dấu vết hành vi: người dùng chỉ bấm vào, chỉ xem, chỉ mua thứ họ thấy thú vị, nên thứ họ
/// GHÉT không để lại dấu nào cả. Không hỏi thì không bao giờ biết — đó là lý do một hệ gợi ý chỉ
/// học từ tín hiệu tích cực sẽ mãi không sửa được một suy đoán sai.
///
/// <b>Không được mâu thuẫn với <see cref="UserFavouriteGenre"/>.</b> Cùng một thể loại vừa thích
/// vừa không thích là một trạng thái vô nghĩa; đường ghi có trách nhiệm gỡ bên kia ra.
/// </summary>
public sealed class UserDislikedGenre : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public int GenreId { get; set; }

    public User User { get; set; } = null!;
    public MusicGenre Genre { get; set; } = null!;
}
