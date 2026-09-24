using System.Reflection;
using FluentAssertions;
using MusicLounge.Application.LoungeShows.Commands.UpdateLoungeShow;

namespace MusicLounge.Tests.Integration.Common;

/// <summary>
/// MLACP-467. Lệnh <c>Update*Command</c> của hệ thống này GHI ĐÈ TOÀN PHẦN: trường nào client không gửi
/// thì thành <c>null</c>/mặc định, không phải "giữ nguyên". Mà client chỉ gửi lại được thứ nó ĐỌC được.
/// Vì vậy mỗi trường của lệnh sửa phải có một đường đọc trong DTO tương ứng; thiếu một trường là mỗi lần
/// người dùng bấm Sửa mà không nhập lại đúng ô đó thì giá trị cũ bị xoá âm thầm — không lỗi, không cảnh
/// báo, không test nào đỏ.
///
/// <para>Lớp lỗi này đã xảy ra thật CHÍN lần: bốn chỗ sửa ở MLACP-467, năm chỗ ở MLACP-469. Phía
/// frontend phát hiện phần lớn khi dựng màn hình sửa; phần còn lại do chính phép quét này tìm ra sau
/// khi nó được sửa để không bỏ qua lệnh nào trong im lặng.</para>
///
/// <para>Ba ví dụ cho thấy vì sao mất dữ liệu mà không ai biết:</para>
/// <list type="bullet">
/// <item><c>UpdateLoungeShowCommand.CategoryId</c> — không DTO đọc nào trả về, sửa buổi hòa nhạc là mất danh mục.</item>
/// <item><c>UpdateLoungeCommand.AtmosphereId</c> — DTO chỉ trả <c>AtmosphereName</c>; tên hiển thị không
/// dùng để dò ngược Id được, đổi tên một cái là mất khớp.</item>
/// <item><c>UpdatePerformerCommand.ContactEmail</c> — không DTO nào trả; mà đó chính là địa chỉ nhận liên
/// kết xác nhận đã nhận tiền ủng hộ, xoá đi là hỏng chặng hai của luồng donate.</item>
/// </list>
///
/// <para>Phép quét này đọc bằng reflection chứ không so chữ: tên trường của lệnh phải xuất hiện trong đồ
/// thị thuộc tính của DTO đọc, kể cả khi nằm lồng trong một DTO con (ví dụ <c>RefundPolicy.RefundPercentage</c>
/// — đọc được thì không tính là thiếu, dù tên ở hai nơi không giống nhau hoàn toàn).</para>
///
/// <para>Khi thêm một trường mới vào lệnh sửa mà quên mở đường đọc, test này đỏ ngay, kèm tên trường và
/// tên DTO phải sửa. Trường nào cố tình không trả về thì khai vào <see cref="CoLyDoKhongTraVe"/> kèm lý do —
/// bắt buộc phải viết lý do, để lần sau đọc lại còn biết vì sao.</para>
/// </summary>
public sealed class EditableFieldsAreReadableTests
{
    /// <summary>
    /// Trường của lệnh sửa mà DTO đọc CỐ Ý không trả về, kèm lý do. Mỗi dòng ở đây là một quyết định,
    /// không phải một ngoại lệ cho tiện.
    /// </summary>
    private static readonly Dictionary<string, string> CoLyDoKhongTraVe = new(StringComparer.Ordinal)
    {
        ["UpdateSystemConfigCommand.Note"] =
            "lý do của LẦN thay đổi này, không phải trạng thái của tham số; nó đi vào SystemConfigHistory " +
            "và mỗi lần sửa phải nhập lại — trả về giá trị cũ mới là sai.",
        ["UpdateSystemConfigCommand.Value"] =
            "giá trị mới do người sửa nhập; giá trị hiện tại đọc qua GET /admin/system-config, không phải " +
            "qua DTO cùng tên lệnh.",
        ["UpdateSystemConfigCommand.Key"] =
            "khoá nằm trên đường dẫn, không phải dữ liệu người dùng nhập lại.",
        ["UpdateAiPreferencesCommand.EnableAiConsent"] =
            "ĐỌC ĐƯỢC, chỉ khác tên: UserProfileDto.AiConsent — phía ghi là hành động bật/tắt, phía đọc là " +
            "trạng thái hiện tại.",
        ["UpdateAiPreferencesCommand.GenreIds"] =
            "ĐỌC ĐƯỢC, chỉ khác tên: UserProfileDto.FavouriteGenreIds.",
        ["UpdateAiPreferencesCommand.MoodIds"] =
            "ĐỌC ĐƯỢC, chỉ khác tên: UserProfileDto.FavouriteMoodIds.",
        ["UpdateAiPreferencesCommand.AtmosphereIds"] =
            "ĐỌC ĐƯỢC, chỉ khác tên: UserProfileDto.FavouriteAtmosphereIds.",
        ["UpdateLoungeShowCommand.CancellationDeadlineHours"] =
            "ĐỌC ĐƯỢC, chỉ khác tên: LoungeShowDetailDto.RefundPolicy.DeadlineHoursBeforeStart. " +
            "Giữ tên khác nhau là có chủ ý — phía đọc là điều khoản công bố cho người mua, phía ghi là " +
            "tham số chủ phòng trà đặt; frontend map hai tên này với nhau.",
    };

    /// <summary>
    /// Tên DTO đọc tương ứng với một thực thể, khi suy từ tên lệnh không ra.
    ///
    /// <para>Bảng này phải phủ HẾT các lệnh sửa: lệnh nào không tra ra DTO thì trước đây bị bỏ qua trong
    /// im lặng, và đó chính là chỗ frontend tìm thấy ba lỗi mà phép quét không thấy (danh mục buổi diễn,
    /// thứ tự diễn, tên tiếng Anh của thể loại). Lệnh chỉ đổi trạng thái thì khai vào
    /// <see cref="KhongPhaiSuaHoSo"/>; không có đường nào để một lệnh sửa lọt qua mà không ai biết.</para>
    /// </summary>
    private static readonly Dictionary<string, string[]> DtoDoc = new(StringComparer.Ordinal)
    {
        ["LoungeShow"] = ["LoungeShowDetailDto"],
        ["Lounge"] = ["LoungeDetailDto"],
        ["Performer"] = ["PerformerDto", "PerformerDetailDto"],
        ["SeatingZone"] = ["SeatingZoneDto"],
        ["BankAccount"] = ["BankAccountDto"],
        ["TicketTier"] = ["TicketTierDto", "TicketTierSummaryDto"],
        ["SubscriptionPackage"] = ["SubscriptionPackageDto"],
        ["SystemConfig"] = ["SystemConfigDto"],
        ["MenuItem"] = ["FnbMenuItemDto"],
        ["Performance"] = ["PerformerSummaryDto"],
        ["AiPreferences"] = ["UserProfileDto"],
        ["MyProfile"] = ["UserProfileDto"],
        ["MyLanguage"] = ["UserProfileDto"],
        // MLACP-469 mở đường đọc riêng cho Admin: danh mục công khai (CatalogItemDto) cố ý chỉ có
        // (Id, Name) và chỉ trả mục đang bật, nên nó KHÔNG phải đường đọc của lệnh sửa.
        ["EventCategory"] = ["AdminEventCategoryDto"],
        ["MusicGenre"] = ["AdminMusicGenreDto"],
        ["Mood"] = ["CatalogItemDto"],
        ["VenueAtmosphere"] = ["CatalogItemDto"],
    };

    /// <summary>Lệnh chỉ đổi trạng thái, không phải sửa hồ sơ — không có gì để đọc lại.</summary>
    private static readonly Dictionary<string, string> KhongPhaiSuaHoSo = new(StringComparer.Ordinal)
    {
        ["UpdateFnbOrderStatusCommand"] = "chuyển trạng thái đơn (Pending → Preparing → …), người dùng " +
                                          "không nhập lại trạng thái cũ.",
    };

    [Fact]
    public void MoiTruongCuaLenhSua_PhaiCoDuongDocTrongDtoTuongUng()
    {
        var assembly = typeof(UpdateLoungeShowCommand).Assembly;

        var lenhSua = assembly.GetTypes()
            .Where(t => t.Name.StartsWith("Update", StringComparison.Ordinal)
                        && t.Name.EndsWith("Command", StringComparison.Ordinal))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        // Chặn "quét trúng số không": sai namespace hay đổi cách đặt tên thì test phải đỏ vì không thấy gì,
        // chứ không được xanh trong im lặng.
        lenhSua.Should().HaveCountGreaterThan(10,
            "quét trúng quá ít lệnh sửa nghĩa là phép quét sai, không phải hệ thống ít lệnh");

        var dtoTheoTen = assembly.GetTypes()
            .Where(t => t.Name.EndsWith("Dto", StringComparison.Ordinal))
            .GroupBy(t => t.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        dtoTheoTen.Should().NotBeEmpty("không tìm thấy DTO nào thì phép quét vô nghĩa");

        var thieu = new List<string>();
        var soLenhDaDoiChieu = 0;

        foreach (var lenh in lenhSua)
        {
            var thucThe = lenh.Name["Update".Length..^"Command".Length];
            var tenDto = DtoDoc.TryGetValue(thucThe, out var chiDinh)
                ? chiDinh
                : new[] { $"{thucThe}DetailDto", $"{thucThe}Dto" };

            var dtos = tenDto
                .Select(n => dtoTheoTen.GetValueOrDefault(n))
                .Where(t => t is not null)
                .Select(t => t!)
                .ToList();

            if (dtos.Count == 0)
            {
                // Trước đây chỗ này lặng lẽ bỏ qua, và đó là lỗ hổng: ba lỗi thật (danh mục buổi diễn,
                // thứ tự diễn, tên tiếng Anh của thể loại) rơi đúng vào đây, phép quét vẫn xanh còn
                // frontend thì gặp mất dữ liệu. Nay không tra ra DTO là phải khai, không được im lặng.
                if (!KhongPhaiSuaHoSo.ContainsKey(lenh.Name))
                    thieu.Add($"{lenh.Name}: không tra ra DTO đọc — khai vào DtoDoc, hoặc vào " +
                              "KhongPhaiSuaHoSo nếu đây là lệnh đổi trạng thái");
                continue;
            }
            soLenhDaDoiChieu++;

            var docDuoc = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dto in dtos) ThuThapThuocTinh(dto, docDuoc, doSau: 0);

            var thamSo = lenh.GetConstructors().OrderByDescending(c => c.GetParameters().Length)
                             .First().GetParameters();

            foreach (var (tham, viTri) in thamSo.Select((t, i) => (t, i)))
            {
                var ten = tham.Name!;
                var khoa = $"{lenh.Name}.{ten}";
                if (CoLyDoKhongTraVe.ContainsKey(khoa)) continue;

                // Định danh của chính bản ghi đang sửa: luôn là tham số đầu tiên và luôn kết thúc bằng "Id"
                // (ShowId, ZoneId, MenuId, TierId…). Nó nằm trên đường dẫn URL, người dùng không nhập lại,
                // nên không cần đọc về.
                if (viTri == 0 && ten.EndsWith("Id", StringComparison.Ordinal)) continue;

                if (docDuoc.Contains(ten)) continue;

                // Danh sách Id: DTO thường trả về danh sách đối tượng {Id, Name} thay vì danh sách Id trần.
                // Đọc lại được thì không tính là thiếu.
                if (ten.EndsWith("Ids", StringComparison.Ordinal) && docDuoc.Contains(ten[..^3] + "s")) continue;

                thieu.Add($"{khoa} (DTO đọc: {string.Join(", ", dtos.Select(d => d.Name))})");
            }
        }

        soLenhDaDoiChieu.Should().BeGreaterThan(3,
            "không đối chiếu được lệnh nào với DTO nghĩa là bảng DtoDoc đã lạc hậu, không phải hệ thống sạch");

        string.Join(Environment.NewLine, thieu).Should().BeEmpty(
            "PUT ghi đè toàn phần, nên client chỉ giữ lại được thứ nó đọc được: mỗi trường ở trên hiện có " +
            "đường GHI mà không có đường ĐỌC, và người dùng bấm Sửa là mất giá trị cũ mà không ai biết. " +
            "Thêm trường vào DTO đọc (và vào hàm ánh xạ), hoặc khai vào CoLyDoKhongTraVe kèm lý do");
    }

    /// <summary>
    /// Gom tên thuộc tính của một DTO, đi xuống các DTO lồng bên trong (và phần tử của danh sách DTO) tối đa
    /// hai cấp. Phải đi xuống vì có dữ liệu chỉ đọc được qua DTO con — ví dụ điều khoản hoàn tiền của buổi
    /// hòa nhạc nằm trong <c>RefundPolicy</c>, đọc được nhưng tên ở hai nơi không trùng nhau.
    /// </summary>
    private static void ThuThapThuocTinh(Type dto, HashSet<string> ket, int doSau)
    {
        if (doSau > 2) return;

        foreach (var p in dto.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            ket.Add(p.Name);

            var kieu = p.PropertyType;
            if (kieu.IsGenericType && kieu.GetGenericArguments().Length == 1)
                kieu = kieu.GetGenericArguments()[0];

            if (kieu.Name.EndsWith("Dto", StringComparison.Ordinal))
                ThuThapThuocTinh(kieu, ket, doSau + 1);
        }
    }
}
