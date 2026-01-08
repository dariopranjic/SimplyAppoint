using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimplyAppoint.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkingHoursTableToDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkingHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    Weekday = table.Column<int>(type: "int", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    OpenTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    CloseTime = table.Column<TimeOnly>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingHours", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkingHours_BusinessId",
                table: "WorkingHours",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingHours_BusinessId_Weekday",
                table: "WorkingHours",
                columns: new[] { "BusinessId", "Weekday" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkingHours");
        }
    }
}
