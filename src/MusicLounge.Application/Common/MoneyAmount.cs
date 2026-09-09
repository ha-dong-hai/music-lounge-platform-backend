using FluentValidation;

namespace MusicLounge.Application.Common;

/// <summary>
/// Đồng Việt Nam không có đơn vị nhỏ hơn 1đ. Không có xu, không có hào — không tờ tiền nào, không
/// lệnh chuyển khoản nào, không cổng thanh toán nào biểu diễn được 0,5đ. Nhưng mọi cột tiền trong
/// hệ thống là <c>decimal(x,2)</c> và mọi validator tiền chỉ nói "lớn hơn 0", nên trước MLACP-332
/// không có chỗ nào phát biểu điều hiển nhiên đó.
///
/// <para><b>Vì sao chuyện này thành mất tiền thật, không chỉ xấu số liệu.</b> Hai tầng xử lý số lẻ
/// theo hai cách ngược nhau, và đã kiểm chứng bằng cách chạy thật với EF Core 8.0.11 + SQL Server
/// (không phải suy luận):</para>
///
/// <list type="bullet">
///   <item>Khi <b>ghi xuống database</b>, EF Core <b>làm tròn</b> về 2 chữ số: <c>10000.999</c> →
///     <c>10001.00</c>.</item>
///   <item>Khi <b>gửi sang VNPay</b>, <c>VnPayService</c> lại <b>cắt cụt</b>: <c>10000.999</c> →
///     thu <c>10000.99</c>.</item>
/// </list>
///
/// <para>Với donate, khách trả tiền theo số VNPay thu, rồi callback quay về và gặp đúng chốt chống
/// giả mạo trong <c>ProcessDonationPaymentCommandHandler</c>: số VNPay báo không khớp số đã lưu, nên
/// callback bị từ chối. Donation nằm mãi ở <c>PendingPayment</c> — <b>khách đã mất tiền mà hệ thống
/// không ghi nhận gì</b>. Bất kỳ số nào có chữ số thập phân thứ ba từ 5 trở lên đều rơi vào đây.</para>
///
/// <para><b>Vì sao chặn ở cửa vào chứ không đi làm tròn cho khớp.</b> Làm cho hai tầng tròn giống
/// nhau thì hết lệch, nhưng đồng lẻ vẫn chảy tiếp vào <see cref="PaymentFeeCalculator"/>, vào sổ
/// cái, vào các đợt quyết toán — sinh ra những khoản không bao giờ chuyển khoản được cho ai. Số
/// tiền vô nghĩa phải bị chặn ở chỗ nó sinh ra. MLACP-332 làm cả hai: chặn ở đây, và sửa
/// <c>VnPayService</c> cắt cụt thành làm tròn đúng như spec VNPay yêu cầu — hai lớp độc lập, để
/// một cửa vào bị bỏ sót trong tương lai không lặp lại đúng lỗi mất tiền này.</para>
///
/// <para>Chốt này chỉ áp cho <b>tiền</b>. Phần trăm (<c>RefundPercentage</c>) và tỉ lệ trong
/// <c>system_config</c> cố ý không đụng tới — chúng có phần lẻ thật sự có nghĩa.</para>
/// </summary>
public static class MoneyAmount
{
    /// <summary>
    /// Số tiền có phải số nguyên đồng không. Dùng <see cref="decimal.Truncate(decimal)"/> chứ không
    /// so với <c>Math.Round</c>: cần biết giá trị có phần lẻ hay không, chứ không phải nó tròn về
    /// đâu.
    /// </summary>
    public static bool IsWholeDong(decimal amount) => decimal.Truncate(amount) == amount;

    /// <summary>Thông báo dùng chung, để mọi cửa vào nói cùng một câu.</summary>
    public const string NotWholeDongMessage =
        "Số tiền phải là số nguyên đồng — đồng Việt Nam không có đơn vị lẻ.";
}

/// <summary>
/// Cho <see cref="MoneyAmount.IsWholeDong"/> đọc lên giống nhau ở mọi validator, thay vì mỗi nơi
/// chép lại một biến thể <c>Must(...)</c> rồi lệch nhau dần.
/// </summary>
public static class MoneyAmountRules
{
    public static IRuleBuilderOptions<T, decimal> MustBeWholeDong<T>(
        this IRuleBuilder<T, decimal> rule)
        => rule.Must(MoneyAmount.IsWholeDong).WithMessage(MoneyAmount.NotWholeDongMessage);

    /// <summary>
    /// Bản cho trường tiền không bắt buộc. <c>null</c> đi qua được — "không nhập" là hợp lệ và do
    /// rule khác quyết định, chốt này chỉ nói về hình dạng của số khi đã có số.
    /// </summary>
    public static IRuleBuilderOptions<T, decimal?> MustBeWholeDong<T>(
        this IRuleBuilder<T, decimal?> rule)
        => rule.Must(a => a is null || MoneyAmount.IsWholeDong(a.Value))
               .WithMessage(MoneyAmount.NotWholeDongMessage);
}
