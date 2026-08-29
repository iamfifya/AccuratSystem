using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Accurat.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedDataAndAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "Address", "Name", "Type" },
                values: new object[] { 2, "в разработке", "ACCURAT - В разработке", 3 });

            migrationBuilder.InsertData(
                table: "Clients",
                columns: new[] { "Id", "CarModel", "CarNumber", "DefaultDiscountPercent", "FullName", "LastVisitDate", "Notes", "Phone", "RegistrationDate", "TotalSpent", "VisitsCount" },
                values: new object[] { 1, "ВАЗ 2105", "В583КВ43", 0m, "Кураедов Дмитрий Витальевич", null, "Разработчик, программист, музыкант и вообще славный парень ахах", "+79996094363", new DateTime(2001, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), 0m, 0 });

            migrationBuilder.InsertData(
                table: "Services",
                columns: new[] { "Id", "Description", "DurationMinutes", "IsActive", "Name", "PriceByBodyType" },
                values: new object[,]
                {
                    { 1, "2-х фазная мойка автомобиля, мойка дисков и резины под щетку, холодный воск, мойка ковриков, чернение", 40, true, "Стандартная мойка кузова", "{\"1\":1150,\"2\":1250,\"3\":1500,\"4\":1750}" },
                    { 2, "Двухфазная мойка, пылесос салона, влажная уборка, очистка стекол, чернение резины, турбосушка, холодный воск", 90, true, "КОМПЛЕКС ACCURAT", "{\"1\":2150,\"2\":2350,\"3\":2700,\"4\":3150}" },
                    { 3, "Внутренняя и внешняя очистка стекол", 15, true, "Чистка стекол", "{\"1\":350,\"2\":350,\"3\":350,\"4\":350}" },
                    { 4, "Тщательная уборка салона пылесосом", 20, true, "Пылесос салона", "{\"1\":350,\"2\":350,\"3\":350,\"4\":350}" },
                    { 5, "Влажная уборка пластика и панелей салона", 15, true, "Влажная уборка", "{\"1\":350,\"2\":350,\"3\":350,\"4\":350}" },
                    { 6, "SHINE SYSTEM - FAST QUARTZ", 20, true, "Кварцевое покрытие", "{\"1\":1000,\"2\":1000,\"3\":1000,\"4\":1000}" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "BranchId", "FullName", "IsActive", "Login", "PasswordHash", "Phone", "Role" },
                values: new object[] { 1, null, "Анастасия", true, "1", "1", "+79877063709", 2 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
