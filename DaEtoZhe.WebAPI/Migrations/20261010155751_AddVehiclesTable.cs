using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DaEtoZhe.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddVehiclesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            /// <summary>
            /// migrationBuilder.DeleteData(
            ///    table: "Branches",
            ///    keyColumn: "Id",
            ///    keyValue: 2);
            /// </summary>

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    Vin = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    LicensePlate = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Make = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Mileage = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EngineType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EngineVolume = table.Column<decimal>(type: "numeric", nullable: false),
                    Transmission = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SellerFullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SellerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SellerPassport = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    RepairCost = table.Column<decimal>(type: "numeric", nullable: false),
                    PartsCost = table.Column<decimal>(type: "numeric", nullable: false),
                    LaborCost = table.Column<decimal>(type: "numeric", nullable: false),
                    ListingPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppraisalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RepairStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RepairEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ListedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SoldDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BuyerFullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    BuyerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BuyerPassport = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Defects = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RepairNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    GeneralNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Vehicles_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Address", "Name", "ServiceLiftsCount", "Type", "WashBaysCount" },
                values: new object[] { "", "OurBusiness", 2, 2, 0 });

            migrationBuilder.UpdateData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "OurBusiness");

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "КОМПЛЕКС OurBusiness");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_BranchId",
                table: "Vehicles",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CompanyId",
                table: "Vehicles",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Address", "Name", "ServiceLiftsCount", "Type", "WashBaysCount" },
                values: new object[] { "ул. Строителей, 54", "ACCURAT - На Строителей", 0, 1, 2 });

            /// <migrationBuilder.InsertData(
            ///     table: "Branches",
            ///     columns: new[] { "Id", "Address", "CompanyId", "IsActive", "Name", "Phone", "ServiceLiftsCount", "TimeZoneId", "Type", "WashBaysCount" },
            ///     values: new object[] { 2, "ул. Луначарского, 26а", 1, false, "ACCURAT - На Луначарского", "", 3, "", 3, 3 });

            migrationBuilder.UpdateData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "ACCURAT GROUP");

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "КОМПЛЕКС ACCURAT");
        }
    }
}
