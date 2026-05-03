using Accurat.WebAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System;

namespace Accurat.WebAPI.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Service> Services => Set<Service>();
        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Shift> Shifts => Set<Shift>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<EmployeeScheduleEntry> EmployeeSchedules => Set<EmployeeScheduleEntry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            // 1. Создаем правило сравнения для словаря (Value Comparer)
            var dictionaryComparer = new ValueComparer<Dictionary<int, decimal>>(
                // Как сравнивать два словаря (сравниваем их элементы)
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                // Как получать хеш-код
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                // Как создавать глубокую копию (снимок) словаря
                c => c.ToDictionary(kv => kv.Key, kv => kv.Value)
            );

            // 2. Применяем конвертер и компаратор к нашему свойству
            modelBuilder.Entity<Service>()
                .Property(s => s.PriceByBodyType)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<int, decimal>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<int, decimal>()
                )
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(dictionaryComparer); // <-- Подключаем компаратор

            base.OnModelCreating(modelBuilder);

            // Добавляем дефолтный филиал, если база пустая
            modelBuilder.Entity<Branch>().HasData(
                new Branch
                {
                    Id = 1,
                    Name = "ACCURAT - Девятый м-н",
                    Address = "ул. Строителей, 54",
                    Type = 1,
                    WashBaysCount = 2,// Например, здесь 2 бокса
                    ServiceLiftsCount = 0
                },
                new Branch
                {
                    Id = 2,
                    Name = "ACCURAT - В разработке",
                    Address = "в разработке",
                    Type = 3,
                    WashBaysCount = 0,    // 2 бокса
                    ServiceLiftsCount = 2 // 2 подъемника
                }
            );

            // Добавляю себя в клиенты
            modelBuilder.Entity<Client>().HasData(
                new Client
                {
                    Id = 1,
                    FullName = "Кураедов Дмитрий Витальевич",
                    Phone = "+79996094363",
                    CarModel = "ВАЗ 2105",
                    CarNumber = "В583КВ43",
                    // Хардкодим статичную дату в формате UTC
                    RegistrationDate = new DateTime(2001, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                    Notes = "Разработчик, программист, музыкант и вообще славный парень ахах"
                }
            );

            // Добавляю базового админа
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    FullName = "Анастасия",
                    Phone = "+79877063709",
                    Login = "1",
                    PasswordHash = "1",
                    Role = 2,
                    IsActive = true,
                    BranchId = null,
                }
            );

            modelBuilder.Entity<EmployeeScheduleEntry>()
                .HasKey(e => new { e.EmployeeId, e.Year, e.Month, e.Day });

            // ===============================
            //      ХАРДКОДИМ ПРАЙС-ЛИСТ
            // ===============================
            modelBuilder.Entity<Service>().HasData(
                new Service
                {
                    Id = 1,
                    Name = "Стандартная мойка кузова",
                    Description = "2-х фазная мойка автомобиля, мойка дисков и резины под щетку, холодный воск, мойка ковриков, чернение",
                    DurationMinutes = 40,
                    IsActive = true,
                    PriceByBodyType = new Dictionary<int, decimal> { { 1, 1150m }, { 2, 1250m }, { 3, 1500m }, { 4, 1750m } }
                }, 
                new Service
                {
                    Id = 2,
                    Name = "КОМПЛЕКС ACCURAT",
                    Description = "Двухфазная мойка, пылесос салона, влажная уборка, очистка стекол, чернение резины, турбосушка, холодный воск",
                    DurationMinutes = 90,
                    IsActive = true,
                    PriceByBodyType = new Dictionary<int, decimal> { { 1, 2150m }, { 2, 2350m }, { 3, 2700m }, { 4, 3150m } }
                },
                new Service
                {
                    Id = 3,
                    Name = "Чистка стекол",
                    Description = "Внутренняя и внешняя очистка стекол",
                    DurationMinutes = 15,
                    IsActive = true,
                    // "от 350" -> сделал легкую градацию по категориям
                    PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } }
                },
                new Service
                {
                    Id = 4,
                    Name = "Пылесос салона",
                    Description = "Тщательная уборка салона пылесосом",
                    DurationMinutes = 20,
                    IsActive = true,
                    PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } }
                },
                new Service
                {
                    Id = 5,
                    Name = "Влажная уборка",
                    Description = "Влажная уборка пластика и панелей салона",
                    DurationMinutes = 15,
                    IsActive = true,
                    PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } }
                },
                new Service
                {
                    Id = 6,
                    Name = "Кварцевое покрытие",
                    Description = "SHINE SYSTEM - FAST QUARTZ",
                    DurationMinutes = 20,
                    IsActive = true,
                    // "от 1000"
                    PriceByBodyType = new Dictionary<int, decimal> { { 1, 1000m }, { 2, 1000m }, { 3, 1000m }, { 4, 1000m } }
                }
            );
        }
    }
}