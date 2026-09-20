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
/// <para>Lớp lỗi này đã xảy ra thật ba lần, do phía frontend phát hiện khi dựng màn hình sửa:</para>
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
        ["EventCategory"] = ["CatalogItemDto"],
        ["MusicGenre"] = ["CatalogItemDto"],
        ["Mood"] = ["CatalogItemDto"],
        ["VenueAtmosphere"] = ["CatalogItemDto"],
    };

    /// <summary>Lệnh chỉ đổi trạng thái, không phải sửa hồ sơ — không có gì để đọc lại.</summary>
    private static readonly Dictionary<string, string> KhongPhaiSuaHoSo = new(StringComparer.Ordinal)
    {
        ["UpdateFnbOrderStatusCommand"] = "chuyển trạng thái đơn (Pending → Preparing → …), người dùng " +
                                          "không nhập lại trạng thái cũ.",
    };

    /// <summary>
    /// Khoảng trống ĐÃ BIẾT: trường có đường ghi mà chưa có đường đọc, đã báo nhưng chưa sửa.
    ///
    /// <para>Đây là danh sách có chốt hai đầu: mục còn trong đây thì phép quét chính bỏ qua, nhưng
    /// <see cref="KhoangTrongDaBiet_PhaiDungNhuDangGhi"/> bắt buộc mỗi mục PHẢI còn là khoảng trống thật.
    /// Sửa xong mà quên xoá khỏi đây là test đỏ — danh sách không phình lên rồi mục nát.</para>
    /// </summary>
    private static readonly Dictionary<string, string> KhoangTrongDaBiet = new(StringComparer.Ordinal)
    {
        ["UpdateEventCategoryCommand.Description"] =
            "GET /catalog/event-categories chỉ trả CatalogItemDto(Id, Name) — sửa danh mục là mất mô tả. " +
            "Frontend báo 20/09/2026; chờ thêm DTO đọc riêng cho Admin.",
        ["UpdateEventCategoryCommand.IsActive"] =
            "Tệ hơn mô tả: truy vấn đọc lọc IsActive == true, nên tắt một danh mục xong thì không màn hình " +
            "nào nhìn thấy nó nữa để bật lại — cửa một chiều. Frontend báo 20/09/2026.",
        ["UpdateMusicGenreCommand.NameEn"] =
            "filter-options và catalog đều trả CatalogItemDto(Id, Name), không có NameEn — sửa thể loại là " +
            "mất tên tiếng Anh. Frontend báo 20/09/2026.",
        ["UpdatePerformanceCommand.OrderIndex"] =
            "PerformerSummaryDto không trả OrderIndex, mà UpdatePerformanceCommand bắt buộc có: client " +
            "phải tự bịa một số hoặc suy từ vị trí trong mảng. Frontend báo 20/09/2026 — và đã dính lỗi " +
            "thật: gửi vị trí trong mảng cho MỘT người, trong khi số đang lưu là 0, 5, 10, nên sửa vai trò " +
            "của người thứ ba lại đẩy họ lên trước người thứ hai. Frontend đang phải chữa bằng cách đánh số " +
            "lại cả danh sách (N lệnh PUT tuần tự, commit 804040d bên kho giao diện). " +
            "KHI THÊM OrderIndex VÀO DTO: xoá dòng này VÀ báo phía giao diện, để họ bỏ cách chữa đó và " +
            "quay lại một lệnh PUT.",
        ["UpdateAiPreferencesCommand.DislikedGenreIds"] =
            "UserProfileDto trả ba danh sách yêu thích nhưng không trả danh sách thể loại bị loại trừ; " +
            "người dùng mở lại trang sở thích là mất phần đã loại trừ.",
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
                if (KhoangTrongDaBiet.ContainsKey(khoa)) continue;   // đã báo, có chốt ở test dưới

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
    /// Chốt đầu kia của <see cref="KhoangTrongDaBiet"/>: mỗi mục trong đó phải CÒN là khoảng trống thật.
    ///
    /// <para>Không có chốt này thì danh sách "đã biết" chỉ có lớn lên: sửa xong không ai xoá, rồi vài tháng
    /// sau không ai còn biết mục nào là lỗi thật, mục nào là rác. Sửa được một chỗ là test này đỏ, kèm câu
    /// nhắc xoá dòng tương ứng — đỏ một lần, rẻ hơn một danh sách mục nát.</para>
    /// </summary>
    [Fact]
    public void KhoangTrongDaBiet_PhaiDungNhuDangGhi()
    {
        var assembly = typeof(UpdateLoungeShowCommand).Assembly;
        var dtoTheoTen = assembly.GetTypes()
            .Where(t => t.Name.EndsWith("Dto", StringComparison.Ordinal))
            .GroupBy(t => t.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        KhoangTrongDaBiet.Should().NotBeEmpty(
            "danh sách rỗng thì chính test này vô nghĩa — nếu đã sửa hết thì xoá luôn cả test");

        var daSua = new List<string>();

        foreach (var (khoa, lyDo) in KhoangTrongDaBiet)
        {
            var (tenLenh, tenTruong) = (khoa[..khoa.IndexOf('.')], khoa[(khoa.IndexOf('.') + 1)..]);
            var lenh = assembly.GetTypes().FirstOrDefault(t => t.Name == tenLenh);
            lenh.Should().NotBeNull($"{tenLenh} không còn tồn tại — xoá dòng '{khoa}' khỏi KhoangTrongDaBiet");

            var thucThe = tenLenh["Update".Length..^"Command".Length];
            var docDuoc = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ten in DtoDoc.GetValueOrDefault(thucThe, [$"{thucThe}DetailDto", $"{thucThe}Dto"]))
                if (dtoTheoTen.TryGetValue(ten, out var dto))
                    ThuThapThuocTinh(dto, docDuoc, doSau: 0);

            // Mục ghi "ĐỌC ĐƯỢC, chỉ khác tên" là ghi chú ánh xạ cho frontend, không phải khoảng trống;
            // nó đúng khi trường KHÔNG có tên y hệt trong DTO, và đó cũng là điều kiện kiểm ở dưới.
            if (docDuoc.Contains(tenTruong))
                daSua.Add($"{khoa} — nay đã đọc được rồi, xoá dòng này đi (lý do đang ghi: {lyDo[..Math.Min(60, lyDo.Length)]}…)");
        }

        string.Join(Environment.NewLine, daSua).Should().BeEmpty(
            "khoảng trống đã được lấp thì phải xoá khỏi KhoangTrongDaBiet, để danh sách luôn là hiện trạng " +
            "chứ không phải lịch sử");
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
