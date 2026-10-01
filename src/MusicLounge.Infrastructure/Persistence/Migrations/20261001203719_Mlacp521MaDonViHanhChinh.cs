using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Mlacp521MaDonViHanhChinh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address_ProvinceCode",
                table: "music_lounges",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address_WardCode",
                table: "music_lounges",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_music_lounges_Address_ProvinceCode_Address_WardCode",
                table: "music_lounges",
                columns: new[] { "Address_ProvinceCode", "Address_WardCode" });

            // MLACP-521: gán mã TỈNH cho phòng trà cũ khi tên thành phố gõ tay khớp CHÍNH XÁC (không phân biệt hoa thường) một
            // trong 89 cách viết đã kê (tên đầy đủ, tên rút gọn của 34 tỉnh QĐ 19/2025, vài viết tắt phổ biến). Tên tỉnh cũ đã
            // sáp nhập (vd "Bình Dương") hay cách viết lạ thì để NULL — chủ phòng trà chọn lại. Mã PHƯỜNG không tự gán: phường cũ
            // đã bị tách/gộp, đoán sai còn tệ hơn để trống.
            migrationBuilder.Sql(@"UPDATE [music_lounges] SET [Address_ProvinceCode] = CASE LOWER(LTRIM(RTRIM([Address_City])))
        WHEN N'thành phố hà nội' THEN '01'
        WHEN N'hà nội' THEN '01'
        WHEN N'tp. hà nội' THEN '01'
        WHEN N'tp hà nội' THEN '01'
        WHEN N'tp.hà nội' THEN '01'
        WHEN N'tỉnh cao bằng' THEN '04'
        WHEN N'cao bằng' THEN '04'
        WHEN N'tỉnh tuyên quang' THEN '08'
        WHEN N'tuyên quang' THEN '08'
        WHEN N'tỉnh điện biên' THEN '11'
        WHEN N'điện biên' THEN '11'
        WHEN N'tỉnh lai châu' THEN '12'
        WHEN N'lai châu' THEN '12'
        WHEN N'tỉnh sơn la' THEN '14'
        WHEN N'sơn la' THEN '14'
        WHEN N'tỉnh lào cai' THEN '15'
        WHEN N'lào cai' THEN '15'
        WHEN N'tỉnh thái nguyên' THEN '19'
        WHEN N'thái nguyên' THEN '19'
        WHEN N'tỉnh lạng sơn' THEN '20'
        WHEN N'lạng sơn' THEN '20'
        WHEN N'thành phố quảng ninh' THEN '22'
        WHEN N'quảng ninh' THEN '22'
        WHEN N'thành phố bắc ninh' THEN '24'
        WHEN N'bắc ninh' THEN '24'
        WHEN N'tỉnh phú thọ' THEN '25'
        WHEN N'phú thọ' THEN '25'
        WHEN N'thành phố hải phòng' THEN '31'
        WHEN N'hải phòng' THEN '31'
        WHEN N'tp. hải phòng' THEN '31'
        WHEN N'tp hải phòng' THEN '31'
        WHEN N'tỉnh hưng yên' THEN '33'
        WHEN N'hưng yên' THEN '33'
        WHEN N'tỉnh ninh bình' THEN '37'
        WHEN N'ninh bình' THEN '37'
        WHEN N'tỉnh thanh hóa' THEN '38'
        WHEN N'thanh hóa' THEN '38'
        WHEN N'tỉnh nghệ an' THEN '40'
        WHEN N'nghệ an' THEN '40'
        WHEN N'tỉnh hà tĩnh' THEN '42'
        WHEN N'hà tĩnh' THEN '42'
        WHEN N'tỉnh quảng trị' THEN '44'
        WHEN N'quảng trị' THEN '44'
        WHEN N'thành phố huế' THEN '46'
        WHEN N'huế' THEN '46'
        WHEN N'thừa thiên huế' THEN '46'
        WHEN N'thừa thiên - huế' THEN '46'
        WHEN N'tp. huế' THEN '46'
        WHEN N'thành phố đà nẵng' THEN '48'
        WHEN N'đà nẵng' THEN '48'
        WHEN N'tp. đà nẵng' THEN '48'
        WHEN N'tp đà nẵng' THEN '48'
        WHEN N'tỉnh quảng ngãi' THEN '51'
        WHEN N'quảng ngãi' THEN '51'
        WHEN N'tỉnh gia lai' THEN '52'
        WHEN N'gia lai' THEN '52'
        WHEN N'tỉnh khánh hòa' THEN '56'
        WHEN N'khánh hòa' THEN '56'
        WHEN N'tỉnh đắk lắk' THEN '66'
        WHEN N'đắk lắk' THEN '66'
        WHEN N'tỉnh lâm đồng' THEN '68'
        WHEN N'lâm đồng' THEN '68'
        WHEN N'thành phố đồng nai' THEN '75'
        WHEN N'đồng nai' THEN '75'
        WHEN N'thành phố hồ chí minh' THEN '79'
        WHEN N'hồ chí minh' THEN '79'
        WHEN N'tp.hcm' THEN '79'
        WHEN N'tp. hcm' THEN '79'
        WHEN N'tp hcm' THEN '79'
        WHEN N'tphcm' THEN '79'
        WHEN N'hcm' THEN '79'
        WHEN N'tp. hồ chí minh' THEN '79'
        WHEN N'tp hồ chí minh' THEN '79'
        WHEN N'tp.hồ chí minh' THEN '79'
        WHEN N'sài gòn' THEN '79'
        WHEN N'tỉnh tây ninh' THEN '80'
        WHEN N'tây ninh' THEN '80'
        WHEN N'tỉnh đồng tháp' THEN '82'
        WHEN N'đồng tháp' THEN '82'
        WHEN N'tỉnh vĩnh long' THEN '86'
        WHEN N'vĩnh long' THEN '86'
        WHEN N'tỉnh an giang' THEN '91'
        WHEN N'an giang' THEN '91'
        WHEN N'thành phố cần thơ' THEN '92'
        WHEN N'cần thơ' THEN '92'
        WHEN N'tp. cần thơ' THEN '92'
        WHEN N'tp cần thơ' THEN '92'
        WHEN N'tỉnh cà mau' THEN '96'
        WHEN N'cà mau' THEN '96'
    END
WHERE [Address_ProvinceCode] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_music_lounges_Address_ProvinceCode_Address_WardCode",
                table: "music_lounges");

            migrationBuilder.DropColumn(
                name: "Address_ProvinceCode",
                table: "music_lounges");

            migrationBuilder.DropColumn(
                name: "Address_WardCode",
                table: "music_lounges");
        }
    }
}
