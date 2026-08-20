using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FFF1_AddUserSecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValueSql (not a fixed defaultValue) so SQL Server evaluates NEWID() once per
            // existing row during the backfill — every pre-migration user gets its own random
            // stamp instead of all of them sharing the same Guid.Empty. New rows going forward
            // always get an explicit value from User.SecurityStamp's C# initializer, so this
            // default is purely a one-time backfill concern, not a steady-state dependency.
            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "users");
        }
    }
}
