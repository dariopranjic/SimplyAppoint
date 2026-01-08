using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimplyAppoint.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeOffTableToDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TimeOffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeOffs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffs_BusinessId",
                table: "TimeOffs",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffs_BusinessId_StartUtc",
                table: "TimeOffs",
                columns: new[] { "BusinessId", "StartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimeOffs");
        }
    }
}
