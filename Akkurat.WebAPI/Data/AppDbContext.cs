// ДОБАВЬ ЭТОТ USING для OutboxMessage (он API-only, не в Contracts)
using Accurat.WebAPI.Models;
using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

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

        // OutboxMessage — API-only, поэтому DbSet остаётся с явным get/set
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<OrderWasher> OrderWashers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка композитного ключа для OrderWasher
            modelBuilder.Entity<OrderWasher>()
                .HasKey(ow => new { ow.OrderId, ow.UserId });

            // Настраиваем связи БЕЗ навигационных свойств в лямбдах (для совместимости)
            modelBuilder.Entity<OrderWasher>()
                .HasOne<Order>()
                .WithMany(o => o.OrderWashers)
                .HasForeignKey(ow => ow.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderWasher>()
                .HasOne<User>()
                .WithMany() // В User нет коллекции OrderWashers — оставляем пустым
                .HasForeignKey(ow => ow.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Конвертер для Dictionary<int, decimal> -> jsonb
            var dictionaryComparer = new ValueComparer<Dictionary<int, decimal>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToDictionary(kv => kv.Key, kv => kv.Value)
            );

            modelBuilder.Entity<Service>()
                .Property(s => s.PriceByBodyType)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<int, decimal>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<int, decimal>()
                )
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(dictionaryComparer);

            // Seed-данные (филиалы, клиенты, услуги) — оставляем как есть
            modelBuilder.Entity<Branch>().HasData(
                new Branch { Id = 1, Name = "ACCURAT - На Строителей", Address = "ул. Строителей, 54", Type = 1, WashBaysCount = 2, ServiceLiftsCount = 0 },
                new Branch { Id = 2, Name = "ACCURAT - На Луначарского", Address = "ул. Луначарского, 26а", Type = 3, WashBaysCount = 3, ServiceLiftsCount = 3 }
            );

            modelBuilder.Entity<Client>().HasData(
                new Client { Id = 1, FullName = "Кураедов Дмитрий Витальевич", Phone = "+79996094363", CarModel = "ВАЗ 2105", CarNumber = "В583КВ43", RegistrationDate = new DateTime(2001, 9, 29, 0, 0, 0, DateTimeKind.Utc), Notes = "Разработчик" }
            );

            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, FullName = "Анастасия", Phone = "+79877063709", Login = "а1", PasswordHash = "1", Role = 2, IsActive = true, BranchId = null }
            );

            modelBuilder.Entity<EmployeeScheduleEntry>()
                .HasKey(e => new { e.EmployeeId, e.Year, e.Month, e.Day });

            // Услуги (прайс-лист) — оставляем как есть
            modelBuilder.Entity<Service>().HasData(
                new Service { Id = 1, Name = "Стандартная мойка кузова", Description = "2-х фазная мойка", DurationMinutes = 40, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 1150m }, { 2, 1250m }, { 3, 1500m }, { 4, 1750m } } },
                new Service { Id = 2, Name = "КОМПЛЕКС ACCURAT", Description = "Двухфазная мойка, пылесос, уборка", DurationMinutes = 90, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 2150m }, { 2, 2350m }, { 3, 2700m }, { 4, 3150m } } },
                new Service { Id = 3, Name = "Чистка стекол", Description = "Внутренняя и внешняя очистка", DurationMinutes = 15, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } } },
                new Service { Id = 4, Name = "Пылесос салона", Description = "Уборка салона", DurationMinutes = 20, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } } },
                new Service { Id = 5, Name = "Влажная уборка", Description = "Уборка пластика", DurationMinutes = 15, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } } },
                new Service { Id = 6, Name = "Кварцевое покрытие", Description = "SHINE SYSTEM", DurationMinutes = 20, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 1000m }, { 2, 1000m }, { 3, 1000m }, { 4, 1000m } } }
            );
        }
    }
}