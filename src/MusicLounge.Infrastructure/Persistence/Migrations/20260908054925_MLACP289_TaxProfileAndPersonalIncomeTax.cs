using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MLACP289_TaxProfileAndPersonalIncomeTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                table: "users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                table: "users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxCodeHash",
                table: "users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TaxProfileSubmittedAt",
                table: "users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TaxProfileVerifiedAt",
                table: "users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxProfileVerifiedBy",
                table: "users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PersonalIncomeTaxWithheld",
                table: "payments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "Thuế GTGT khấu trừ tại nguồn (5% — tỷ lệ cho DỊCH VỤ theo NĐ 117/2025/NĐ-CP). Chỉ khấu trừ cho hộ/cá nhân kinh doanh; doanh nghiệp tự kê khai.");

            migrationBuilder.InsertData(
                table: "system_config",
                columns: new[] { "Id", "ConfigKey", "ConfigValue", "DataType", "Description", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 32, "personal_income_tax_rate", "0", "Decimal", "Thuế TNCN khấu trừ tại nguồn. NĐ 117/2025/NĐ-CP quy định 2% cho DỊCH VỤ của cá nhân cư trú; seed bằng 0 để việc bật khấu trừ là một quyết định vận hành có ghi lý do, không phải hệ quả của một lần triển khai. Chỉ áp cho hộ/cá nhân kinh doanh.", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.CreateIndex(
                name: "IX_users_TaxCodeHash",
                table: "users",
                column: "TaxCodeHash",
                unique: true,
                filter: "[TaxCodeHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_TaxCodeHash",
                table: "users");

            migrationBuilder.DeleteData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TaxCodeHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TaxProfileSubmittedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TaxProfileVerifiedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TaxProfileVerifiedBy",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PersonalIncomeTaxWithheld",
                table: "payments");

            migrationBuilder.UpdateData(
                table: "system_config",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "Thuế GTGT khấu trừ tại nguồn (5% — tỷ lệ cho DỊCH VỤ theo NĐ 117/2025/NĐ-CP). Chưa khấu trừ TNCN.");
        }
    }
}
