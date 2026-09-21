using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.CustomCriteria.DTOs;

namespace MusicLounge.Application.CustomCriteria.Queries.GetLoungeCustomCriteria;

/// <param name="IncludeInactive">
/// Lấy cả tiêu chí đã tắt. Mặc định false vì đường dùng chính là dựng màn hình tạo buổi diễn.
///
/// <para>Bắt buộc phải có kể từ khi tiêu chí tắt được (MLACP-474): danh sách mặc định lọc bỏ tiêu chí
/// tắt, nên không có tham số này thì tắt xong là tiêu chí biến mất khỏi mọi màn hình và KHÔNG CÒN ĐƯỜNG
/// NÀO bật lại — cái nút tắt sẽ thành cửa một chiều.</para>
/// </param>
public sealed record GetLoungeCustomCriteriaQuery(int LoungeId, bool IncludeInactive = false)
    : IQuery<IReadOnlyList<CustomCriteriaDto>>;
