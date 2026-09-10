using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class FixOrderServiceItemForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderServiceItems_Orders_OrderId1",
                table: "OrderServiceItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderServiceItems_OrderId1",
                table: "OrderServiceItems");

            migrationBuilder.DropColumn(
                name: "OrderId1",
                table: "OrderServiceItems");

            migrationBuilder.UpdateData(
                table: "OrderStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "ColorHex",
                value: "#27AE60");

            migrationBuilder.UpdateData(
                table: "OrderStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "ColorHex",
                value: "#E74C3C");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderId1",
                table: "OrderServiceItems",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "OrderStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "ColorHex",
                value: "#2ECC71");

            migrationBuilder.UpdateData(
                table: "OrderStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "ColorHex",
                value: "#95A5A6");

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceItems_OrderId1",
                table: "OrderServiceItems",
                column: "OrderId1");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderServiceItems_Orders_OrderId1",
                table: "OrderServiceItems",
                column: "OrderId1",
                principalTable: "Orders",
                principalColumn: "Id");
        }
    }
}
