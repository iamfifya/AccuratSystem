using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class NightShiftAndDiscountPercentageAndPercentageForAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDiscountRulesEnabled",
                table: "TenantFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Shifts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DayShiftAdminPercentage",
                table: "CompanySettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NightShiftWasherPercentage",
                table: "CompanySettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "CompanyId",
                keyValue: 1,
                columns: new[] { "DayShiftAdminPercentage", "NightShiftWasherPercentage" },
                values: new object[] { 2.5m, 50.0m });

            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsDiscountRulesEnabled",
                value: true);

            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsDiscountRulesEnabled",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDiscountRulesEnabled",
                table: "TenantFeatures");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "DayShiftAdminPercentage",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "NightShiftWasherPercentage",
                table: "CompanySettings");
        }
    }
}
