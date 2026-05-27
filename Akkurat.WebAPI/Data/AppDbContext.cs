// ДОБАВЬ ЭТОТ USING для OutboxMessage (он API-only, не в Contracts)
using Accurat.WebAPI.Models;
using AccuratSystem.Contracts.DTOs;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
using Akkurat.WebAPI.Models;
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

        public DbSet<OrderExpense> OrderExpenses { get; set; }
        public DbSet<OrderTimelineEntry> OrderTimelineEntries { get; set; }
        public DbSet<OrderServiceItem> OrderServiceItems { get; set; }

        // OutboxMessage — API-only, поэтому DbSet остаётся с явным get/set
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<OrderWasher> OrderWashers { get; set; }
        public DbSet<AccuratSystem.Contracts.Models.OrderStatusHistory> OrderStatusHistories { get; set; }
        public DbSet<TenantFeature> TenantFeatures { get; set; }
        public DbSet<UpsellSuggestion> UpsellSuggestions { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка композитного ключа для OrderWasher
            modelBuilder.Entity<OrderWasher>()
                .HasKey(ow => new { ow.OrderId, ow.UserId });

            // Настраиваем связи БЕЗ навигационных свойств в лямбдах (для совместимости)
            modelBuilder.Entity<OrderWasher>()
                .HasOne(ow => ow.Order)
                .WithMany(o => o.OrderWashers) // Убедись, что в классе Order есть public List<OrderWasher> OrderWashers { get; set; }
                .HasForeignKey(ow => ow.OrderId);

            modelBuilder.Entity<OrderWasher>()
                .HasOne<User>()
                .WithMany() // В User нет коллекции OrderWashers — оставляем пустым
                .HasForeignKey(ow => ow.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // === НОВЫЕ СУЩНОСТИ ДЛЯ СЕРВИСА ===

            // 1. OrderExpense - внутренние затраты по заказу
            modelBuilder.Entity<OrderExpense>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Индексы для быстрого поиска
                entity.HasIndex(e => e.OrderId);
                entity.HasIndex(e => new { e.OrderId, e.Category });

                // Конвертер для enum -> string в БД (совместимо с PostgreSQL jsonb)
                entity.Property(e => e.Category)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                // Связь с Order
                entity.HasOne<Order>()
                    .WithMany() // В Order нет навигационной коллекции для C# 7.3 совместимости
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 2. OrderTimelineEntry - лента событий заказа
            modelBuilder.Entity<OrderTimelineEntry>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Индексы для быстрой загрузки ленты
                entity.HasIndex(e => e.OrderId);
                entity.HasIndex(e => new { e.OrderId, e.Timestamp }); // составной для сортировки

                // Конвертер для enum -> string
                entity.Property(e => e.EntryType)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                // Связь с Order
                entity.HasOne<Order>()
                    .WithMany()
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 3. OrderServiceItem - связь заказа с услугами (для истории изменения цен)
            modelBuilder.Entity<OrderServiceItem>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Индексы
                entity.HasIndex(e => e.OrderId);
                entity.HasIndex(e => e.ServiceId);
                entity.HasIndex(e => new { e.OrderId, e.ServiceId });

                // Связи
                entity.HasOne<Order>()
                    .WithMany()
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<Service>()
                    .WithMany()
                    .HasForeignKey(e => e.ServiceId)
                    .OnDelete(DeleteBehavior.Restrict); // Не удалять услугу, если она используется в истории
            });

            // 4. Order - новые поля для сервиса
            modelBuilder.Entity<Order>(entity =>
            {
                // Индекс для быстрого поиска по статусу + дате (для отчётов)
                entity.HasIndex(o => new { o.Status, o.Time });
                entity.HasIndex(o => new { o.BranchId, o.Department, o.Time });

                // Конвертер для строковых статусов (если захочешь перевести на enum позже)
                // entity.Property(o => o.Status).HasMaxLength(50);
            });

            // 5. ServiceCategory enum -> string конвертер
            modelBuilder.Entity<Service>()
                .Property(s => s.ServiceCategory)
                .HasConversion<string>()
                .HasMaxLength(20);


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
                .HasKey(e => new { e.EmployeeId, e.BranchId, e.Year, e.Month, e.Day });

            // Услуги (прайс-лист) — оставляем как есть
            modelBuilder.Entity<Service>().HasData(
                new Service { Id = 1, Name = "Стандартная мойка кузова", Description = "2-х фазная мойка", DurationMinutes = 40, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 1150m }, { 2, 1250m }, { 3, 1500m }, { 4, 1750m } } },
                new Service { Id = 2, Name = "КОМПЛЕКС ACCURAT", Description = "Двухфазная мойка, пылесос, уборка", DurationMinutes = 90, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 2150m }, { 2, 2350m }, { 3, 2700m }, { 4, 3150m } } },
                new Service { Id = 3, Name = "Чистка стекол", Description = "Внутренняя и внешняя очистка", DurationMinutes = 15, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } } },
                new Service { Id = 4, Name = "Пылесос салона", Description = "Уборка салона", DurationMinutes = 20, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } } },
                new Service { Id = 5, Name = "Влажная уборка", Description = "Уборка пластика", DurationMinutes = 15, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } } },
                new Service { Id = 6, Name = "Кварцевое покрытие", Description = "SHINE SYSTEM", DurationMinutes = 20, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 1000m }, { 2, 1000m }, { 3, 1000m }, { 4, 1000m } } }
            );

            // Создаем индекс для быстрой выборки текущего статуса заказа
            modelBuilder.Entity<AccuratSystem.Contracts.Models.OrderStatusHistory>()
                .HasIndex(osh => new { osh.OrderId, osh.EndTime });
            // Говорим Entity Framework игнорировать это поле, чтобы он не искал его в таблице Orders
            modelBuilder.Entity<AccuratSystem.Contracts.Models.Order>().Ignore(o => o.CurrentStatusStartTime);

            modelBuilder.Entity<TenantFeature>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.BranchId).IsUnique(); // Один филиал = один набор прав
                entity.Property(e => e.IsUpsellEnabled).HasDefaultValue(false);
                entity.Property(e => e.IsStorageEnabled).HasDefaultValue(false);
                entity.Property(e => e.IsCrmMarketingEnabled).HasDefaultValue(false);
                entity.Property(e => e.IsTelegramBossEnabled).HasDefaultValue(false);
            });

            modelBuilder.Entity<UpsellSuggestion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TriggerServiceId);
            });

            modelBuilder.Entity<UpsellSuggestion>().HasData(
    new UpsellSuggestion
    {
        Id = 1,
        TriggerServiceId = 1, // Стандартная мойка
        SuggestedServiceId = 6, // Кварцевое покрытие
        Message = "Клиент выбрал стандартную мойку. Предложите покрыть кузов кварцем для защиты от грязи и блеска!",
        BonusAmount = 150m // Премия админу/мойщику за допродажу
    },
    new UpsellSuggestion
    {
        Id = 2,
        TriggerServiceId = 2, // Комплекс
        SuggestedServiceId = 3, // Чистка стекол
        Message = "В комплекс не входит антидождь/глубокая чистка стекол. Отличный шанс предложить эту услугу!",
        BonusAmount = 50m
    }
);

            // И не забудь включить сам модуль для филиала (иначе UserSession.IsFeatureEnabled вернет false)
            modelBuilder.Entity<TenantFeature>().HasData(
                new TenantFeature { Id = 1, BranchId = 1, IsUpsellEnabled = true },
                new TenantFeature { Id = 2, BranchId = 2, IsUpsellEnabled = true }
            );

        }
    }
}