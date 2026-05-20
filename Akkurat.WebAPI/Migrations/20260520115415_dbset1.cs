using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class dbset1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_BranchId",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "ServiceCategory",
                table: "Services",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "OrderExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CostPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    ClientPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderExpenses_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderServiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    ActualPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    PriceNote = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderServiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderServiceItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderServiceItems_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderTimelineEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    RelatedEntityId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTimelineEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderTimelineEntries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 1,
                column: "ServiceCategory",
                value: "Wash");

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 2,
                column: "ServiceCategory",
                value: "Wash");

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 3,
                column: "ServiceCategory",
                value: "Wash");

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 4,
                column: "ServiceCategory",
                value: "Wash");

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 5,
                column: "ServiceCategory",
                value: "Wash");

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 6,
                column: "ServiceCategory",
                value: "Wash");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BranchId_Department_Time",
                table: "Orders",
                columns: new[] { "BranchId", "Department", "Time" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_Time",
                table: "Orders",
                columns: new[] { "Status", "Time" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderExpenses_OrderId",
                table: "OrderExpenses",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderExpenses_OrderId_Category",
                table: "OrderExpenses",
                columns: new[] { "OrderId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceItems_OrderId",
                table: "OrderServiceItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceItems_OrderId_ServiceId",
                table: "OrderServiceItems",
                columns: new[] { "OrderId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderServiceItems_ServiceId",
                table: "OrderServiceItems",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTimelineEntries_OrderId",
                table: "OrderTimelineEntries",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTimelineEntries_OrderId_Timestamp",
                table: "OrderTimelineEntries",
                columns: new[] { "OrderId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderExpenses");

            migrationBuilder.DropTable(
                name: "OrderServiceItems");

            migrationBuilder.DropTable(
                name: "OrderTimelineEntries");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BranchId_Department_Time",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_Time",
                table: "Orders");

            migrationBuilder.AlterColumn<int>(
                name: "ServiceCategory",
                table: "Services",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 1,
                column: "ServiceCategory",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 2,
                column: "ServiceCategory",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 3,
                column: "ServiceCategory",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 4,
                column: "ServiceCategory",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 5,
                column: "ServiceCategory",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 6,
                column: "ServiceCategory",
                value: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BranchId",
                table: "Orders",
                column: "BranchId");
        }
    }
}
