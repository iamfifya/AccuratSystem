using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyIdToUpsell : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "UpsellSuggestions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrderId1",
                table: "OrderServiceItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "OrderServiceItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.CreateIndex(
                name: "IX_UpsellSuggestions_CompanyId",
                table: "UpsellSuggestions",
                column: "CompanyId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderServiceItems_Orders_OrderId1",
                table: "OrderServiceItems");

            migrationBuilder.DropIndex(
                name: "IX_UpsellSuggestions_CompanyId",
                table: "UpsellSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_OrderServiceItems_OrderId1",
                table: "OrderServiceItems");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "UpsellSuggestions");

            migrationBuilder.DropColumn(
                name: "OrderId1",
                table: "OrderServiceItems");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "OrderServiceItems");
        }
    }
}
