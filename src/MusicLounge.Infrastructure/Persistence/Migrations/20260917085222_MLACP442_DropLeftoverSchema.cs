using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// MLACP-442. Gỡ 3 bảng và 8 cột mà model EF của repo này không có — sót lại từ một bản sao repo khác từng deploy
    /// lên cùng database (cùng nguồn với 4 job định kỳ mồ côi gỡ ở MLACP-440). EF chỉ đọc cột nó biết nên chúng không
    /// gây lỗi, nhưng ai đọc schema sẽ tưởng hệ thống có theo dõi lỗi gửi thông báo, có đối soát, có luồng hoàn tiền
    /// chuyển tay — trong khi không có gì cả.
    ///
    /// Viết tay chứ không scaffold: model không chứa các đối tượng này nên `migrations add` sinh ra migration rỗng.
    /// Mỗi lệnh có điều kiện tồn tại để chạy được cả trên database chưa từng có chúng (máy dev, database test).
    /// Cột có ràng buộc mặc định tên tự sinh (DF__…) nên phải tra tên động rồi xoá ràng buộc trước khi xoá cột.
    ///
    /// Dữ liệu mất (đã ghi lại trong J:/MVP/ML_FE/deploy_log.md trước khi chạy): 3 dòng uploaded_files trỏ tới file đã
    /// mất; donations.Tax = 2500 của donate #1 đã huỷ; refund_requests.RequiresManualTransfer = 1 ở yêu cầu #1;
    /// CompletionStatus = 'NotRequested'. Down dựng lại CẤU TRÚC rỗng, không khôi phục dữ liệu — cần dữ liệu thì phục
    /// hồi database theo thời điểm (Azure SQL giữ 7 ngày).
    /// </summary>
    public partial class MLACP442_DropLeftoverSchema : Migration
    {
        private static readonly (string Table, string Column)[] LeftoverColumns =
        [
            ("complaints", "TargetGuid"),
            ("donations", "Tax"),
            ("music_lounges", "RejectionReason"),
            ("refund_requests", "GatewayRefundResponseCode"),
            ("refund_requests", "RequiresManualTransfer"),
            ("users", "PendingEmail"),
            ("venue_tour_scenes", "CompletedByAi"),
            ("venue_tour_stitch_attempts", "CompletionStatus"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in LeftoverColumns)
                migrationBuilder.Sql($"""
                    IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
                    BEGIN
                        DECLARE @rangBuoc sysname;
                        SELECT @rangBuoc = dc.name
                        FROM sys.default_constraints dc
                        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
                        IF @rangBuoc IS NOT NULL EXEC('ALTER TABLE dbo.{table} DROP CONSTRAINT [' + @rangBuoc + ']');
                        ALTER TABLE dbo.{table} DROP COLUMN [{column}];
                    END
                    """);

            // uploaded_files có khoá ngoại tới users; DROP TABLE tự gỡ khoá ngoại của chính nó.
            foreach (var table in new[] { "push_failure_logs", "push_failure_alert_states", "uploaded_files" })
                migrationBuilder.Sql($"IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL DROP TABLE dbo.{table};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Chỉ dựng lại cấu trúc (bảng rỗng, cột NULL/mặc định) — đúng kiểu dữ liệu như trước khi xoá.
            migrationBuilder.Sql("""
                IF OBJECT_ID('dbo.push_failure_alert_states', 'U') IS NULL
                    CREATE TABLE dbo.push_failure_alert_states (
                        Id int NOT NULL IDENTITY(1,1) CONSTRAINT PK_push_failure_alert_states PRIMARY KEY,
                        LastAlertedAt datetimeoffset NOT NULL);
                """);
            migrationBuilder.Sql("""
                IF OBJECT_ID('dbo.push_failure_logs', 'U') IS NULL
                    CREATE TABLE dbo.push_failure_logs (
                        Id int NOT NULL IDENTITY(1,1) CONSTRAINT PK_push_failure_logs PRIMARY KEY,
                        UserId int NOT NULL,
                        ErrorCode nvarchar(64) NOT NULL,
                        CreatedAt datetimeoffset NOT NULL);
                """);
            migrationBuilder.Sql("""
                IF OBJECT_ID('dbo.uploaded_files', 'U') IS NULL
                    CREATE TABLE dbo.uploaded_files (
                        Id int NOT NULL IDENTITY(1,1) CONSTRAINT PK_uploaded_files PRIMARY KEY,
                        UploaderUserId int NOT NULL,
                        Url nvarchar(500) NOT NULL,
                        Kind nvarchar(20) NOT NULL,
                        CreatedAt datetimeoffset NOT NULL,
                        CONSTRAINT FK_uploaded_files_users_UploaderUserId FOREIGN KEY (UploaderUserId) REFERENCES dbo.users (Id) ON DELETE CASCADE);
                """);

            foreach (var (table, column, kieu) in new[]
                     {
                         ("complaints", "TargetGuid", "uniqueidentifier NULL"),
                         ("donations", "Tax", "decimal(18,2) NOT NULL CONSTRAINT DF_donations_Tax DEFAULT (0.0)"),
                         ("music_lounges", "RejectionReason", "nvarchar(max) NULL"),
                         ("refund_requests", "GatewayRefundResponseCode", "nvarchar(20) NULL"),
                         ("refund_requests", "RequiresManualTransfer", "bit NOT NULL CONSTRAINT DF_refund_requests_RequiresManualTransfer DEFAULT (0)"),
                         ("users", "PendingEmail", "nvarchar(256) NULL"),
                         ("venue_tour_scenes", "CompletedByAi", "bit NOT NULL CONSTRAINT DF_venue_tour_scenes_CompletedByAi DEFAULT (0)"),
                         ("venue_tour_stitch_attempts", "CompletionStatus", "nvarchar(20) NOT NULL CONSTRAINT DF_venue_tour_stitch_attempts_CompletionStatus DEFAULT (N'NotRequested')"),
                     })
                migrationBuilder.Sql(
                    $"IF COL_LENGTH('dbo.{table}', '{column}') IS NULL ALTER TABLE dbo.{table} ADD [{column}] {kieu};");
        }
    }
}
