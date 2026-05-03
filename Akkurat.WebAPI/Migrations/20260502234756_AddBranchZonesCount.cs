using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchZonesCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceLiftsCount",
                table: "Branches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WashBaysCount",
                table: "Branches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ServiceLiftsCount", "WashBaysCount" },
                values: new object[] { 0, 2 });

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ServiceLiftsCount", "WashBaysCount" },
                values: new object[] { 2, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceLiftsCount",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "WashBaysCount",
                table: "Branches");
        }
    }
}
