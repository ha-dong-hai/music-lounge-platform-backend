using System.Globalization;
using System.Text.Json;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.CustomCriteria;

/// <summary>
/// Đối chiếu giá trị gắn cho một buổi hòa nhạc với KIỂU DỮ LIỆU mà chính tiêu chí đó khai.
///
/// <para>Trước đây máy chủ nhận bất kỳ chuỗi nào: tiêu chí khai kiểu Boolean vẫn ghi được chữ bất kỳ,
/// kiểu Range vẫn ghi được chữ. Kiểm ở phía giao diện là lớp tiện cho người dùng, không phải bảo đảm —
/// ai gọi thẳng giao diện lập trình vẫn ghi rác vào được, và rác trong cột giá trị thì MỌI màn hình đọc
/// lên đều phải chịu: ô chọn hiện một giá trị không có trong danh sách, ô số hiện chữ. Sửa ở phía giao
/// diện lúc đó là vá triệu chứng.</para>
///
/// <para>Chỉ chặn cái chắc chắn sai. Khi không đọc được danh sách lựa chọn thì KHÔNG bịa ra luật: từ chối
/// dựa trên một quy tắc mình không đọc được còn tệ hơn là cho qua. Lối thoát đó CHỈ dành cho dòng tạo
/// trước MLACP-472 — từ đó trở đi <c>CreateCustomCriteriaCommandValidator</c> bắt buộc tiêu chí Select
/// phải có danh sách đọc được, nên tiêu chí mới không rơi vào nhánh này. Hai bên dùng CHUNG các hàm đọc
/// dưới đây, để "validator cho tạo" và "đối chiếu đọc được" không bao giờ trôi ra khỏi nhau.</para>
/// </summary>
public static class CustomCriteriaValue
{
    /// <summary>Lý do giá trị không hợp lệ; <c>null</c> nghĩa là hợp lệ.</summary>
    public static string? LoiNeuCo(CustomCriteriaDataType dataType, string? options, string value)
    {
        // Dữ liệu ghi trước đây có thể ở dạng JSON đã đóng gói ("\"true\""). Gỡ một lớp nháy kép trước
        // khi đối chiếu, để ràng buộc mới không từ chối đúng thứ hệ thống từng tự sinh ra.
        var v = GoMotLopNhayKep(value).Trim();

        switch (dataType)
        {
            case CustomCriteriaDataType.Boolean:
                return bool.TryParse(v, out _)
                    ? null
                    : "chỉ nhận true hoặc false.";

            case CustomCriteriaDataType.Range:
                if (!decimal.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var so))
                    return "phải là một con số.";

                var khoang = Khoang(options);
                if (khoang is null) return null;   // không đọc được khoảng thì chỉ đòi là số

                var (min, max) = khoang.Value;
                if (min is not null && so < min) return $"phải lớn hơn hoặc bằng {min}.";
                if (max is not null && so > max) return $"phải nhỏ hơn hoặc bằng {max}.";
                return null;

            case CustomCriteriaDataType.Select:
                var luaChon = DanhSachLuaChon(options);
                if (luaChon is null || luaChon.Count == 0) return null;   // không đọc được thì không bịa luật
                return luaChon.Contains(v, StringComparer.Ordinal)
                    ? null
                    : $"phải là một trong: {string.Join(", ", luaChon)}.";

            // Text: độ dài đã được kiểm ở validator của lệnh; nội dung thì không có gì để đối chiếu.
            default:
                return null;
        }
    }

    /// <summary>
    /// Dạng chuẩn của giá trị trước khi ghi xuống cơ sở dữ liệu.
    ///
    /// <para>Chỉ áp cho Boolean, và có lý do cụ thể: <c>bool.TryParse</c> nhận cả "True"/"TRUE", nên cùng
    /// một ý "đúng" có thể nằm trong cột dưới ba dạng khác nhau. Màn hình nào dựng ô chọn bằng hai lựa
    /// chọn chữ thường sẽ không khớp được dòng ghi "True" — ô hiện trống như thể chưa đặt, mà lệnh ghi là
    /// THAY THẾ TOÀN BỘ nên lần Lưu kế tiếp xoá luôn giá trị đó, không ai thấy. Chuẩn hoá ở đây là chặn
    /// tại gốc: mỗi nơi đọc tự hạ chữ thường thì chỉ vá được đúng màn hình ấy, còn ứng dụng nhân viên và
    /// mọi bên gọi sau này vẫn dính. (Phát hiện khi đối chiếu với phiên làm giao diện, 20/09/2026.)</para>
    ///
    /// <para>KHÔNG chuẩn hoá Select/Text: giá trị ở đó là chuỗi do người dùng đặt, đối chiếu Select dùng
    /// so khớp chính xác, và tự ý sửa chuỗi của người ta là làm hỏng dữ liệu chứ không phải dọn dẹp.</para>
    /// </summary>
    public static string ChuanHoa(CustomCriteriaDataType dataType, string value)
        => dataType == CustomCriteriaDataType.Boolean
            && bool.TryParse(GoMotLopNhayKep(value).Trim(), out var b)
            ? b ? "true" : "false"
            : value;

    /// <summary>Danh sách lựa chọn đọc được từ Options; <c>null</c> khi không đọc được.</summary>
    public static IReadOnlyList<string>? DanhSachLuaChon(string? options)
    {
        if (string.IsNullOrWhiteSpace(options)) return null;
        try
        {
            using var doc = JsonDocument.Parse(options);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            var ket = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) return null;   // dạng lạ thì không đối chiếu
                ket.Add(item.GetString()!);
            }
            return ket;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Khoảng min/max đọc được từ Options; <c>null</c> khi không đọc được hoặc không khai biên nào.</summary>
    public static (decimal? Min, decimal? Max)? Khoang(string? options)
    {
        if (string.IsNullOrWhiteSpace(options)) return null;
        try
        {
            using var doc = JsonDocument.Parse(options);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            decimal? min = doc.RootElement.TryGetProperty("min", out var mi) && mi.TryGetDecimal(out var m) ? m : null;
            decimal? max = doc.RootElement.TryGetProperty("max", out var ma) && ma.TryGetDecimal(out var x) ? x : null;
            return min is null && max is null ? null : (min, max);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Options có phải một đối tượng JSON không. Tiêu chí Range lấy biên từ các khoá <c>min</c>/<c>max</c>,
    /// nên một mảng như <c>[1,2,3]</c> là khai sai chỗ — không có biên nào đọc được và cũng không ai đọc ra
    /// được ý định. Tách riêng khỏi <see cref="Khoang"/> vì hàm đó trả <c>null</c> cho cả hai trường hợp
    /// "không đọc được" và "đọc được nhưng chỉ khai step".
    /// </summary>
    public static bool LaDoiTuongJson(string? options)
    {
        if (string.IsNullOrWhiteSpace(options)) return false;
        try
        {
            using var doc = JsonDocument.Parse(options);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string GoMotLopNhayKep(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;
}
