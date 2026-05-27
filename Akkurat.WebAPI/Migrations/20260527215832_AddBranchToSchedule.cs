using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchToSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_EmployeeSchedules",
                table: "EmployeeSchedules");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "EmployeeSchedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmployeeSchedules",
                table: "EmployeeSchedules",
                columns: new[] { "EmployeeId", "BranchId", "Year", "Month", "Day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_EmployeeSchedules",
                table: "EmployeeSchedules");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "EmployeeSchedules");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmployeeSchedules",
                table: "EmployeeSchedules",
                columns: new[] { "EmployeeId", "Year", "Month", "Day" });
        }
    }
}
