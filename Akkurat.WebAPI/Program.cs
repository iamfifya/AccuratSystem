using Accurat.WebAPI.Data;
using Accurat.WebAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore; // Подключаем Scalar

var builder = WebApplication.CreateBuilder(args);

// 1. Стандартные сервисы
builder.Services.AddControllers();

// 2. РОДНОЙ движок OpenAPI от Microsoft (вместо SwaggerGen)
builder.Services.AddOpenApi();

// 3. Твоя база данных
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 1. Создаем специальный строитель источника данных
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

// 2. ВКЛЮЧАЕМ ТУ САМУЮ МАГИЮ для работы со словарями в JSONB
dataSourceBuilder.EnableDynamicJson();

var dataSource = dataSourceBuilder.Build();

// 3. Регистрируем DbContext, используя созданный dataSource
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource));

builder.Services.AddSignalR();

var app = builder.Build();

// 4. Настраиваем визуальный интерфейс
if (app.Environment.IsDevelopment())
{
    // Генерирует сам файл описания (openapi/v1.json)
    app.MapOpenApi();

    // Рисует красивую страницу по адресу /scalar/v1
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<Accurat.WebAPI.Hubs.AppHub>("/hubs/app");

app.Run();