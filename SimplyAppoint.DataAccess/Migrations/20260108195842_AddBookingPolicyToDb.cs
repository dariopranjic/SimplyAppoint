using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimplyAppoint.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPolicyToDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingPolicies",
                columns: table => new
                {
                    BusinessId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlotIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    AdvanceNoticeMinutes = table.Column<int>(type: "int", nullable: false),
                    CancellationWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxAdvanceDays = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingPolicies", x => x.BusinessId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingPolicies");
        }
    }
}
