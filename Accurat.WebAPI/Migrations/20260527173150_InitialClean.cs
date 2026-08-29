using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialClean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderWashers_Orders_OrderId1",
                table: "OrderWashers");

            migrationBuilder.DropIndex(
                name: "IX_OrderWashers_OrderId1",
                table: "OrderWashers");

            migrationBuilder.DropColumn(
                name: "OrderId1",
                table: "OrderWashers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderId1",
                table: "OrderWashers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderWashers_OrderId1",
                table: "OrderWashers",
                column: "OrderId1");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderWashers_Orders_OrderId1",
                table: "OrderWashers",
                column: "OrderId1",
                principalTable: "Orders",
                principalColumn: "Id");
        }
    }
}
