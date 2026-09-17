using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchTimeZoneId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Branches",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1,
                column: "TimeZoneId",
                value: "");

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 2,
                column: "TimeZoneId",
                value: "");

            migrationBuilder.UpdateData(
                table: "UpsellSuggestions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CompanyId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "UpsellSuggestions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CompanyId",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Branches");

            migrationBuilder.UpdateData(
                table: "UpsellSuggestions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CompanyId",
                value: 0);

            migrationBuilder.UpdateData(
                table: "UpsellSuggestions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CompanyId",
                value: 0);
        }
    }
}
