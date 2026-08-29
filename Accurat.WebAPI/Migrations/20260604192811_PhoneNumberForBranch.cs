using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class PhoneNumberForBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Branches",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1,
                column: "Phone",
                value: "");

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 2,
                column: "Phone",
                value: "");

            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsServicesEnabled",
                value: true);

            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsServicesEnabled",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Branches");

            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsServicesEnabled",
                value: false);

            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsServicesEnabled",
                value: false);
        }
    }
}
