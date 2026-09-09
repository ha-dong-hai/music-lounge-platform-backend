using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// BR-31 (MLACP-309). Mốc đóng đợt bán vé trở thành tuỳ chọn, kèm ngưỡng "giờ nhận khách cuối"
    /// ở cấp nền tảng.
    ///
    /// Đi lên không cần chuyển đổi dữ liệu: mọi dòng đang có sẵn một mốc và giữ nguyên mốc đó.
    /// Điều đổi với dữ liệu cũ là ngưỡng giờ nhận khách cuối áp lên cả những mốc đã đặt sẵn — một
    /// đợt bán từng chạy tới sát giờ tan buổi diễn nay dừng sớm hơn. Đó chính là mục đích.
    ///
    /// Migration này từng bị mất: lệnh `dotnet ef migrations remove --no-build` đọc assembly cũ nên
    /// xoá nhầm file, và thay vào đó để lại hai migration nháp tên ZZ_*. Khôi phục ở MLACP-314.
    /// </summary>
    public partial class MLACP309_OptionalSaleEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SaleEnd",
                table: "ticket_prices",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            // Ngưỡng bảo vệ khách hàng ở cấp nền tảng nên phải nằm trong system_config để Admin
            // nhìn thấy và chỉnh được, không phải một hằng số chôn trong code.
            migrationBuilder.InsertData(
                table: "system_config",
                columns: new[] { "Id", "ConfigKey", "ConfigValue", "DataType", "Description", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 33, "ticket_last_entry_minutes", "60", "Integer", "Last-entry cutoff: minutes of the show that must still remain for a ticket to be sold — BR-31, matches Eventbrite's general-admission default", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 33);

            // Lấp giá trị trước rồi mới siết cột lại. EF sinh sẵn defaultValue là DateTimeOffset
            // .MinValue cho bước siết này, nghĩa là mọi đợt bán đang bỏ trống mốc đóng sẽ nhận năm
            // 0001 — tức đóng bán vĩnh viễn, không bán được vé nữa. Điền giờ buổi diễn bắt đầu vào
            // đó là một mốc có nghĩa và luôn nằm trước giờ nhận khách cuối.
            migrationBuilder.Sql("""
                UPDATE tp
                SET tp.SaleEnd = s.ScheduledStart
                FROM ticket_prices tp
                INNER JOIN ticket_tiers tt ON tt.Id = tp.TierId
                INNER JOIN lounge_shows s ON s.Id = tt.LoungeShowId
                WHERE tp.SaleEnd IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SaleEnd",
                table: "ticket_prices",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);
        }
    }
}
