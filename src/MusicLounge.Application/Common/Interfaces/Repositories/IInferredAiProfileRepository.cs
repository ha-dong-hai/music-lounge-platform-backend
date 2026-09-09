namespace MusicLounge.Application.Common.Interfaces.Repositories;

/// <summary>
/// Truy cập tới những gì hệ thống đã SUY RA về một người dùng, tách khỏi những gì họ TỰ KHAI.
///
/// Sự phân biệt này là toàn bộ lý do lớp truy cập riêng tồn tại. Sở thích người dùng tự chọn ở bước
/// onboarding là dữ liệu của họ, họ giao cho hệ thống có chủ đích. Còn điểm số hành vi, kết quả gợi
/// ý đã tính sẵn, và trọng số tiêu chí suy từ hành vi là những thứ hệ thống tự dựng lên về họ. Rút
/// lại sự đồng ý phải xoá nhóm thứ hai, và KHÔNG được đụng vào nhóm thứ nhất.
///
/// <c>UserEventScore</c> dùng khoá kép (UserId + ShowId) nên không nằm sau repository chung được —
/// và chính vì thế nó đã bị bỏ sót ở đường xoá dữ liệu cá nhân, với một chú thích nói rằng nó
/// "không chứa nội dung định danh". Chú thích đó sai: cột Breakdown lưu JSON ghi rõ người này đã dự
/// buổi diễn nào, chấm mấy sao, có donate hay không.
/// </summary>
public interface IInferredAiProfileRepository
{
    /// <summary>
    /// Dàn hàng xoá toàn bộ hồ sơ hệ thống đã suy ra về người dùng này: điểm số hành vi theo từng
    /// buổi diễn, gợi ý đã tính sẵn, và trọng số tiêu chí riêng.
    ///
    /// Chỉ dàn hàng, KHÔNG lưu — theo đúng giao kèo chung của tầng này, lời gọi SaveChangesAsync
    /// của người gọi mới là cái ghi xuống, để việc xoá nằm cùng một giao dịch với phần còn lại.
    /// </summary>
    Task ForgetAsync(int userId, CancellationToken ct = default);
}
