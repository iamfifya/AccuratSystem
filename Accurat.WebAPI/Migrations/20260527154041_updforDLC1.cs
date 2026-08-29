using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class updforDLC1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    IsUpsellEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsServicesEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsCrmMarketingEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsTelegramBossEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantFeatures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantFeatures_BranchId",
                table: "TenantFeatures",
                column: "BranchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantFeatures");
        }
    }
}
