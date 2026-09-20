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
/// <para>Chỉ chặn cái chắc chắn sai. Khi không đọc được danh sách lựa chọn (dòng cũ tạo trước khi có
/// ràng buộc JSON ở lệnh tạo tiêu chí) thì KHÔNG bịa ra luật: từ chối dựa trên một quy tắc mình không
/// đọc được còn tệ hơn là cho qua.</para>
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

                var khoang = DocKhoang(options);
                if (khoang is null) return null;   // không đọc được khoảng thì chỉ đòi là số

                var (min, max) = khoang.Value;
                if (min is not null && so < min) return $"phải lớn hơn hoặc bằng {min}.";
                if (max is not null && so > max) return $"phải nhỏ hơn hoặc bằng {max}.";
                return null;

            case CustomCriteriaDataType.Select:
                var luaChon = DocDanhSachLuaChon(options);
                if (luaChon is null || luaChon.Count == 0) return null;   // không đọc được thì không bịa luật
                return luaChon.Contains(v, StringComparer.Ordinal)
                    ? null
                    : $"phải là một trong: {string.Join(", ", luaChon)}.";

            // Text: độ dài đã được kiểm ở validator của lệnh; nội dung thì không có gì để đối chiếu.
            default:
                return null;
        }
    }

    private static string GoMotLopNhayKep(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;

    private static (decimal? Min, decimal? Max)? DocKhoang(string? options)
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

    private static IReadOnlyList<string>? DocDanhSachLuaChon(string? options)
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
}
