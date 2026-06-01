using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class BranchIdToComanyIDForTenantFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BranchId",
                table: "TenantFeatures",
                newName: "CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_TenantFeatures_BranchId",
                table: "TenantFeatures",
                newName: "IX_TenantFeatures_CompanyId");

            migrationBuilder.AlterColumn<string>(
                name: "PayloadJson",
                table: "OutboxMessages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "OutboxMessages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "OutboxMessages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantFeatures_Companies_CompanyId",
                table: "TenantFeatures",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenantFeatures_Companies_CompanyId",
                table: "TenantFeatures");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "TenantFeatures",
                newName: "BranchId");

            migrationBuilder.RenameIndex(
                name: "IX_TenantFeatures_CompanyId",
                table: "TenantFeatures",
                newName: "IX_TenantFeatures_BranchId");

            migrationBuilder.AlterColumn<string>(
                name: "PayloadJson",
                table: "OutboxMessages",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "OutboxMessages",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "OutboxMessages",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
