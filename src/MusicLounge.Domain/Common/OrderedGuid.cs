namespace MusicLounge.Domain.Common;

/// <summary>
/// MLACP-515 (D-19: mọi khoá chính là GUID). GUID TĂNG DẦN theo thời điểm tạo — trên CẢ SQL Server lẫn SQLite.
///
/// <para><b>Vì sao không dùng thứ có sẵn.</b> Khoảng 24 truy vấn sắp "mới nhất trước" THEO Id (vì SQLite dùng trong test
/// không ORDER BY được DateTimeOffset). GUID ngẫu nhiên phá thứ tự đó. Nhưng hai CSDL so GUID theo hai chiều ngược nhau:
/// SQL Server (uniqueidentifier) xét 6 byte CUỐI trước; SQLite lưu GUID dạng chữ nên xét ký tự ĐẦU trước. NEWSEQUENTIALID /
/// SequentialGuidValueGenerator chỉ có thứ tự trên SQL Server; UUIDv7 chỉ có thứ tự khi so theo chữ — chọn một cái là test và
/// production sắp khác nhau.</para>
///
/// <para><b>Cách làm.</b> Cùng một bộ đếm 48 bit đặt ở CẢ nhóm đầu (12 ký tự hex đầu) lẫn nhóm cuối (12 ký tự hex cuối):
/// <c>CCCCCCCC-CCCC-8RRR-8RRR-CCCCCCCCCCCC</c>. Bộ đếm = số mili-giây Unix, đơn điệu tăng trong tiến trình (trùng mili-giây thì
/// +1), nên thứ tự đúng ở cả hai phía. Giữa là 24 bit ngẫu nhiên + nibble phiên bản 8 (RFC 9562 "tuỳ biến") và biến thể RFC.</para>
///
/// <para><b>Trần đã biết.</b> Đơn điệu trong MỘT tiến trình; App Service gói B1 chạy 1 instance nên đủ. Nhiều instance thì hai
/// máy có thể sinh cùng mili-giây — vẫn KHÔNG trùng nhờ 24 bit ngẫu nhiên, chỉ thứ tự giữa hai máy trong cùng mili-giây là
/// không xác định (không ảnh hưởng nghiệp vụ).</para>
/// </summary>
public static class OrderedGuid
{
    private static long _last;

    public static Guid New()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long value;
        while (true)
        {
            var last = Interlocked.Read(ref _last);
            value = Math.Max(last + 1, now);
            if (Interlocked.CompareExchange(ref _last, value, last) == last) break;
        }
        return FromCounter(value, Random.Shared.Next(1 << 24));
    }

    /// <summary>
    /// MLACP-515: GUID của một bản ghi CŨ (khoá int trước khi chuyển) — MỘT công thức cho cả dữ liệu seed trong mã lẫn
    /// migration chuyển dữ liệu thật trên Azure, nên "Jazz" (music_genres #1) có cùng GUID ở mọi nơi và mọi khoá ngoại tính
    /// lại khớp nhau. Bộ đếm = id cũ (giữ đúng thứ tự cũ; mọi bản ghi mới sinh bằng <see cref="New"/> mang bộ đếm mili-giây
    /// nên luôn đứng sau). 24 bit phân biệt = băm FNV-1a của tên bảng, để id 1 của hai bảng khác nhau không trùng GUID.
    /// <para>Công thức này PHẢI giữ nguyên vĩnh viễn — đổi nó là mọi khoá ngoại của dữ liệu đã chuyển lệch hết.</para>
    /// </summary>
    public static Guid FromLegacy(string table, long legacyId)
    {
        uint h = 2166136261;
        foreach (var ch in table) { h ^= ch; h *= 16777619; }
        return FromCounter(legacyId, (int)(h & 0xFFFFFF));
    }

    /// <summary>Dựng GUID từ một bộ đếm 48 bit và 24 bit phân biệt — dùng cả cho dữ liệu cũ khi chuyển khoá int sang GUID
    /// (bộ đếm = id cũ, nên thứ tự cũ được giữ và mọi bản ghi mới đều đứng sau).</summary>
    public static Guid FromCounter(long counter, int discriminator)
    {
        if (counter is < 0 or > 0xFFFF_FFFF_FFFF) throw new ArgumentOutOfRangeException(nameof(counter));
        var c = counter.ToString("x12");
        var d = (discriminator & 0xFFFFFF).ToString("x6");
        return Guid.Parse($"{c[..8]}-{c[8..]}-8{d[..3]}-8{d[3..]}-{c}");
    }
}
