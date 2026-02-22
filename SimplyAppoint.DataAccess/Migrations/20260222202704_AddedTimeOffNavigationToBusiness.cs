using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimplyAppoint.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddedTimeOffNavigationToBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffs_Businesses_BusinessId",
                table: "TimeOffs",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffs_Businesses_BusinessId",
                table: "TimeOffs");
        }
    }
}
