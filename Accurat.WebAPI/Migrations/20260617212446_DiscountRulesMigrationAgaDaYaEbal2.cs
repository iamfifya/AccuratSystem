using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class DiscountRulesMigrationAgaDaYaEbal2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IsDiscountRulesEnabled", "IsServicesEnabled" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "IsDiscountRulesEnabled", "IsServicesEnabled" },
                values: new object[] { false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IsDiscountRulesEnabled", "IsServicesEnabled" },
                values: new object[] { true, true });

            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "IsDiscountRulesEnabled", "IsServicesEnabled" },
                values: new object[] { true, true });
        }
    }
}
