using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCarCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarCategories_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CarCategories",
                columns: new[] { "Id", "CompanyId", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, 1, "Категория 1 (Легковая)", 1 },
                    { 2, 1, "Категория 2 (Универсал/Кроссовер)", 2 },
                    { 3, 1, "Категория 3 (Внедорожник)", 3 },
                    { 4, 1, "Категория 4 (Микроавтобус)", 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarCategories_CompanyId",
                table: "CarCategories",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarCategories");
        }
    }
}
