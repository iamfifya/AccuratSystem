using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class NewDLCIsReputationEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReputationEnabled",
                table: "TenantFeatures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsReputationEnabled",
                value: false);

            migrationBuilder.UpdateData(
                table: "TenantFeatures",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsReputationEnabled",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReputationEnabled",
                table: "TenantFeatures");
        }
    }
}
