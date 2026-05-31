using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyToServicesAndClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. СНАЧАЛА СОЗДАЕМ КОЛОНКИ (у всех будет 0)
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Clients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // 2. СРАЗУ ПОСЛЕ ЭТОГО ЗАПИСЫВАЕМ ЕДИНИЧКИ (исправляем 0 на 1)
            migrationBuilder.Sql("UPDATE \"Services\" SET \"CompanyId\" = 1;");
            migrationBuilder.Sql("UPDATE \"Clients\" SET \"CompanyId\" = 1;");

            // 3. (Блоки UpdateData с нулями лучше просто убрать, SQL-запрос выше и так всё обновит)

            // 4. ТЕПЕРЬ ВЕШАЕМ ИНДЕКСЫ И КЛЮЧИ
            migrationBuilder.CreateIndex(
                name: "IX_Services_CompanyId",
                table: "Services",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CompanyId",
                table: "Clients",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Companies_CompanyId",
                table: "Clients",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Services_Companies_CompanyId",
                table: "Services",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Companies_CompanyId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Services_Companies_CompanyId",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Services_CompanyId",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Clients_CompanyId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Clients");
        }
    }
}
