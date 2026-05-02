using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGhostBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "Address", "Name", "Type" },
                values: new object[,]
                {
                    { 1, "ул. Строителей, 54", "ACCURAT - Девятый м-н", 1 },
                    { 2, "в разработке", "ACCURAT - В разработке", 3 }
                });
        }
    }
}
