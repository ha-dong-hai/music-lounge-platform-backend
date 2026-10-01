using System.Data.SqlTypes;
using FluentAssertions;
using MusicLounge.Domain.Common;

namespace MusicLounge.Tests.Integration.Unit;

/// <summary>
/// MLACP-515 (D-19). Khoảng 24 truy vấn sắp "mới nhất trước" THEO Id. Sau khi đổi khoá sang GUID, thứ tự đó chỉ còn đúng
/// nếu GUID tăng dần theo thời điểm tạo — và phải đúng trên CẢ SQL Server (production, so uniqueidentifier theo 6 byte cuối
/// trước) lẫn SQLite (test, lưu GUID dạng chữ nên so ký tự đầu trước). SqlGuid của .NET so đúng như SQL Server.
/// </summary>
public sealed class OrderedGuidUnitTests
{
    private static List<Guid> SinhLienTiep(int n) => Enumerable.Range(0, n).Select(_ => OrderedGuid.New()).ToList();

    [Fact]
    public void New_TangDanTheoCachSqlServerSo()
    {
        var ds = SinhLienTiep(500);

        ds.Select(g => new SqlGuid(g)).Should().BeInAscendingOrder(Comparer<SqlGuid>.Create((a, b) => a.CompareTo(b)),
            "cột uniqueidentifier của SQL Server phải sắp đúng thứ tự tạo");
    }

    [Fact]
    public void New_TangDanTheoChuoi_NhuSqlite()
    {
        var ds = SinhLienTiep(500);

        ds.Select(g => g.ToString()).Should().BeInAscendingOrder(StringComparer.Ordinal,
            "SQLite lưu GUID dạng chữ — test sắp theo Id phải ra cùng thứ tự với production");
    }

    [Fact]
    public void New_KhongTrung()
        => SinhLienTiep(5000).Should().OnlyHaveUniqueItems();

    [Fact]
    public void FromLegacy_XacDinh_GiuThuTuIdCu_VaKhacNhauGiuaCacBang()
    {
        OrderedGuid.FromLegacy("music_genres", 1).Should().Be(OrderedGuid.FromLegacy("music_genres", 1),
            "seed trong mã và migration dữ liệu phải ra CÙNG một GUID cho cùng một bản ghi cũ");
        OrderedGuid.FromLegacy("music_genres", 1).Should().NotBe(OrderedGuid.FromLegacy("moods", 1));

        var cu = Enumerable.Range(1, 300).Select(i => OrderedGuid.FromLegacy("lounge_shows", i)).ToList();
        cu.Select(g => new SqlGuid(g)).Should().BeInAscendingOrder(Comparer<SqlGuid>.Create((a, b) => a.CompareTo(b)));
        cu.Select(g => g.ToString()).Should().BeInAscendingOrder(StringComparer.Ordinal);

        // Bản ghi cũ (bộ đếm = id cũ, rất nhỏ) luôn đứng TRƯỚC mọi bản ghi mới (bộ đếm = mili-giây Unix).
        new SqlGuid(cu[^1]).CompareTo(new SqlGuid(OrderedGuid.New())).Should().BeNegative();
    }
}
