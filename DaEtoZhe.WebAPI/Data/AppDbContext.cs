using DaEtoZhe.Contracts.DTOs;
using DaEtoZhe.Contracts.Enums;
using DaEtoZhe.Contracts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DaEtoZhe.WebAPI.Time;
using OrderStatuses = DaEtoZhe.Contracts.Models.OrderStatuses;

namespace DaEtoZhe.WebAPI.Data
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
        public DbSet<DaEtoZhe.Contracts.Models.OrderStatusHistories> OrderStatusHistories { get; set; }
        public DbSet<TenantFeature> TenantFeatures { get; set; }
        public DbSet<UpsellSuggestion> UpsellSuggestions { get; set; }
        public DbSet<Role> Roles { get; set; } // Новая таблица с должностями
        public DbSet<Company> Companies { get; set; }
        public DbSet<CarCategory> CarCategories { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<DaEtoZhe.Contracts.Models.OrderStatuses> OrderStatuses { get; set; }
        public DbSet<CompanySettings> CompanySettings { get; set; }
        public DbSet<DiscountRule> DiscountRules { get; set; }
        public DbSet<CashReconciliation> CashReconciliations => Set<CashReconciliation>();

        public DbSet<ShiftTimelineEntry> ShiftTimelineEntries => Set<ShiftTimelineEntry>();

        // Складской учет
        public DbSet<StockCategory> StockCategories => Set<StockCategory>();
        public DbSet<StockItem> StockItems => Set<StockItem>();

        // Складской учет: документы и движения
        public DbSet<StockBalance> StockBalances => Set<StockBalance>();
        public DbSet<StockDocument> StockDocuments => Set<StockDocument>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();

        // Складской учет: нормы списания на услуги
        public DbSet<ServiceStockNorm> ServiceStockNorms => Set<ServiceStockNorm>();

        // Автомобили для сервиса и продажи
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Связь 1 ко многим: Компания -> Филиалы
            modelBuilder.Entity<Branch>()
                .HasOne(b => b.Company)
                .WithMany(c => c.Branches)
                .HasForeignKey(b => b.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            // 1. Явно указываем связь User -> Role
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany()
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // 2. Сидируем (заполняем) таблицу ролей при создании БД
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Директор" },
                new Role { Id = 2, Name = "Администратор" },
                new Role { Id = 3, Name = "Мойщик" },
                new Role { Id = 4, Name = "Сотрудник сервиса" }
            );

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

                // ИСПРАВЛЕНО: явно указываем навигационное свойство Order.OrderServiceItems
                entity.HasOne<Order>()
                    .WithMany(o => o.OrderServiceItems)  // ← ДОБАВЛЕНО
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<Service>()
                    .WithMany()
                    .HasForeignKey(e => e.ServiceId)
                    .OnDelete(DeleteBehavior.Restrict);
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

            // Связь: у одной компании может быть много категорий авто
            modelBuilder.Entity<CarCategory>()
                .HasOne(c => c.Company)
                .WithMany()
                .HasForeignKey(c => c.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<Service>()
                .Property(s => s.PriceByBodyType)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<int, decimal>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<int, decimal>()
                )
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(dictionaryComparer);

            // Настройка связи с Company
            modelBuilder.Entity<PaymentMethod>()
                .HasOne(p => p.Company)
                .WithMany()
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Настраиваем связь 1-к-1. CompanyId является и первичным ключом, и внешним.
            modelBuilder.Entity<CompanySettings>()
                .HasKey(cs => cs.CompanyId);

            modelBuilder.Entity<CompanySettings>()
                .HasOne(cs => cs.Company)
                .WithOne()
                .HasForeignKey<CompanySettings>(cs => cs.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);



            // Seed-данные (филиалы, клиенты, услуги) — оставляем как есть
            // 1. Создаем первую компанию-владельца
            modelBuilder.Entity<Company>().HasData(
                new Company
                {
                    Id = 1,
                    Name = "OurBusiness",
                    IsActive = true,
                    RegistrationDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            // 2. Добавляем CompanyId = 1 в наши филиалы
            modelBuilder.Entity<Branch>().HasData(
                new Branch { Id = 1, CompanyId = 1, Name = "OurBusiness", Address = "", Type = 2, WashBaysCount = 0, ServiceLiftsCount = 2 }
            );

            modelBuilder.Entity<Client>().HasData(
                new Client { Id = 1, FullName = "Кураедов Дмитрий Витальевич", Phone = "+79996094363", CarModel = "ВАЗ 2105", CarNumber = "В583КВ43", RegistrationDate = new DateTime(2001, 9, 29, 0, 0, 0, DateTimeKind.Utc), Notes = "Разработчик" }
            );

            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, FullName = "Анастасия", Phone = "+79877063709", Login = "а1", PasswordHash = "1", RoleId = 2, IsActive = true, BranchId = null, CompanyId = 1 }
            );

            modelBuilder.Entity<User>()
                .HasOne(u => u.Company)
                .WithMany()
                .HasForeignKey(u => u.CompanyId)
                .OnDelete(DeleteBehavior.Restrict); // Нельзя удалить компанию, если в ней есть сотрудники


            modelBuilder.Entity<EmployeeScheduleEntry>()
                .HasKey(e => new { e.EmployeeId, e.BranchId, e.Year, e.Month, e.Day });

            // Добавляем наши привычные 4 категории и привязываем их к компании OurBusiness (CompanyId = 1)
            modelBuilder.Entity<CarCategory>().HasData(
                new CarCategory { Id = 1, CompanyId = 1, Name = "Категория 1 (Легковая)", SortOrder = 1 },
                new CarCategory { Id = 2, CompanyId = 1, Name = "Категория 2 (Универсал/Кроссовер)", SortOrder = 2 },
                new CarCategory { Id = 3, CompanyId = 1, Name = "Категория 3 (Внедорожник)", SortOrder = 3 },
                new CarCategory { Id = 4, CompanyId = 1, Name = "Категория 4 (Микроавтобус)", SortOrder = 4 }
            );

            // Услуги (прайс-лист) — оставляем как есть
            modelBuilder.Entity<Service>().HasData(
                new Service { Id = 1, Name = "Стандартная мойка кузова", Description = "2-х фазная мойка", DurationMinutes = 40, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 1150m }, { 2, 1250m }, { 3, 1500m }, { 4, 1750m } }, CompanyId = 1 },
                new Service { Id = 2, Name = "КОМПЛЕКС OurBusiness", Description = "Двухфазная мойка, пылесос, уборка", DurationMinutes = 90, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 2150m }, { 2, 2350m }, { 3, 2700m }, { 4, 3150m } }, CompanyId = 1 },
                new Service { Id = 3, Name = "Чистка стекол", Description = "Внутренняя и внешняя очистка", DurationMinutes = 15, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } }, CompanyId = 1 },
                new Service { Id = 4, Name = "Пылесос салона", Description = "Уборка салона", DurationMinutes = 20, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } }, CompanyId = 1 },
                new Service { Id = 5, Name = "Влажная уборка", Description = "Уборка пластика", DurationMinutes = 15, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 350m }, { 2, 350m }, { 3, 350m }, { 4, 350m } }, CompanyId = 1 },
                new Service { Id = 6, Name = "Кварцевое покрытие", Description = "SHINE SYSTEM", DurationMinutes = 20, IsActive = true, PriceByBodyType = new Dictionary<int, decimal> { { 1, 1000m }, { 2, 1000m }, { 3, 1000m }, { 4, 1000m } }, CompanyId = 1 }
            );

            // Создаем индекс для быстрой выборки текущего статуса заказа
            modelBuilder.Entity<DaEtoZhe.Contracts.Models.OrderStatusHistories>()
                .HasIndex(osh => new { osh.OrderId, osh.EndTime });
            // Говорим Entity Framework игнорировать это поле, чтобы он не искал его в таблице Orders
            modelBuilder.Entity<DaEtoZhe.Contracts.Models.Order>().Ignore(o => o.CurrentStatusStartTime);

            modelBuilder.Entity<TenantFeature>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CompanyId).IsUnique(); // Один филиал = один набор прав
                entity.Property(e => e.IsUpsellEnabled).HasDefaultValue(false);
                entity.Property(e => e.IsServicesEnabled).HasDefaultValue(false);
                entity.Property(e => e.IsCrmMarketingEnabled).HasDefaultValue(false);
                entity.Property(e => e.IsTelegramBossEnabled).HasDefaultValue(false);
                entity.Property(e => e.IsReputationEnabled).HasDefaultValue(false);
            });

            modelBuilder.Entity<UpsellSuggestion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TriggerServiceId);
                entity.HasIndex(e => e.CompanyId); // ДОБАВЛЕНО: Индекс для быстрой фильтрации по компаниям
            });

            // Добавляем твои 4 стандартных способа оплаты (плюс "Не указано" для дефолта)
            modelBuilder.Entity<PaymentMethod>().HasData(
                new PaymentMethod { Id = 1, CompanyId = 1, Name = "Не указано", SortOrder = 1 },
                new PaymentMethod { Id = 2, CompanyId = 1, Name = "Наличные", SortOrder = 2 },
                new PaymentMethod { Id = 3, CompanyId = 1, Name = "Карта", SortOrder = 3 },
                new PaymentMethod { Id = 4, CompanyId = 1, Name = "Перевод", SortOrder = 4 },
                new PaymentMethod { Id = 5, CompanyId = 1, Name = "QR-код", SortOrder = 5 }
            );

            modelBuilder.Entity<OrderStatuses>().HasData(
                new OrderStatuses { Id = 1, CompanyId = 1, Name = "В работе", Icon = "🟢", ColorHex = "#3498DB", SortOrder = 1 },
                new OrderStatuses { Id = 2, CompanyId = 1, Name = "Выполнен", Icon = "✅", ColorHex = "#27AE60", SortOrder = 2 },  // ИЗМЕНЕНО: было #2ECC71
                new OrderStatuses { Id = 3, CompanyId = 1, Name = "Отменен", Icon = "❌", ColorHex = "#E74C3C", SortOrder = 3 }   // ИЗМЕНЕНО: было #95A5A6
            );

            // Добавляем настройки для компании OurBusiness (CompanyId = 1)
            modelBuilder.Entity<CompanySettings>().HasData(
                new CompanySettings
                {
                    CompanyId = 1,
                    CompanySharePercentage = 65m,
                    DefaultAppointmentDuration = 60
                }
            );

            modelBuilder.Entity<UpsellSuggestion>().HasData(
            new UpsellSuggestion
            {
                Id = 1,
                CompanyId = 1,
                TriggerServiceId = 1, // Стандартная мойка
                SuggestedServiceId = 6, // Кварцевое покрытие
                Message = "Клиент выбрал стандартную мойку. Предложите покрыть кузов кварцем для защиты от грязи и блеска!",
                BonusAmount = 150m // Премия админу/мойщику за допродажу
            },
            new UpsellSuggestion
            {
                Id = 2,
                CompanyId = 1,
                TriggerServiceId = 2, // Комплекс
                SuggestedServiceId = 3, // Чистка стекол
                Message = "В комплекс не входит антидождь/глубокая чистка стекол. Отличный шанс предложить эту услугу!",
                BonusAmount = 50m
            }
            );

            // И не забудь включить сам модуль для филиала (иначе UserSession.IsFeatureEnabled вернет false)
            modelBuilder.Entity<TenantFeature>().HasData(
                new TenantFeature { Id = 1, CompanyId = 1, IsUpsellEnabled = true, IsInventoryEnabled = true },
                new TenantFeature { Id = 2, CompanyId = 2, IsUpsellEnabled = true }
            );

            // === СВЕРКА КАССЫ (X-ОТЧЕТ) ===
            modelBuilder.Entity<CashReconciliation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ShiftId);

                entity.HasOne(e => e.Shift)
                    .WithMany()
                    .HasForeignKey(e => e.ShiftId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // === ЛЕНТА СОБЫТИЙ СМЕНЫ ===
            modelBuilder.Entity<ShiftTimelineEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ShiftId);
                entity.HasIndex(e => new { e.ShiftId, e.Timestamp });

                entity.HasOne(e => e.Shift)
                    .WithMany()
                    .HasForeignKey(e => e.ShiftId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // === СКЛАД: КАТЕГОРИИ (словарь компании) ===
            modelBuilder.Entity<StockCategory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.CompanyId, e.Name }).IsUnique();
                entity.HasOne<Company>()
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // === СКЛАД: НОМЕНКЛАТУРА ===
            modelBuilder.Entity<StockItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.CompanyId, e.Name });

                // Артикул уникален в рамках компании, но только если он задан
                entity.HasIndex(e => new { e.CompanyId, e.Article }).IsUnique()
                    .HasFilter("\"Article\" IS NOT NULL AND \"Article\" <> ''");

                entity.Property(e => e.Unit).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.PurchaseUnit).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.PurchaseRatio).HasPrecision(18, 3);
                entity.Property(e => e.MinStock).HasPrecision(18, 3);
                entity.Property(e => e.LastPurchaseCost).HasPrecision(18, 2);

                entity.HasOne(e => e.Category)
                    .WithMany()
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne<Company>()
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // === СКЛАД: ОСТАТКИ (уникальны в паре позиция+филиал) ===
            modelBuilder.Entity<StockBalance>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ItemId, e.BranchId }).IsUnique();
                entity.Property(e => e.Quantity).HasPrecision(18, 3);
                entity.Property(e => e.AvgCost).HasPrecision(18, 2);
                entity.HasOne(e => e.Item).WithMany()
                    .HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Branch).WithMany()
                    .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Cascade);
            });

            // === СКЛАД: ДОКУМЕНТЫ ===
            modelBuilder.Entity<StockDocument>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.CompanyId, e.Number }).IsUnique();
                entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
                entity.HasOne(e => e.Branch).WithMany()
                    .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
            });

            // === СКЛАД: ДВИЖЕНИЯ ===
            modelBuilder.Entity<StockMovement>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ItemId, e.BranchId });
                entity.HasIndex(e => e.DocumentId);
                entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.Quantity).HasPrecision(18, 3);
                entity.Property(e => e.CostPrice).HasPrecision(18, 2);
                entity.HasOne(e => e.Item).WithMany()
                    .HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Document).WithMany(d => d.Movements)
                    .HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
            });

            // === СКЛАД: НОРМЫ СПИСАНИЯ (услуга → позиция) ===
            modelBuilder.Entity<ServiceStockNorm>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ServiceId, e.ItemId }).IsUnique();
                entity.Property(e => e.Quantity).HasPrecision(18, 3);
                entity.HasOne(e => e.Service).WithMany()
                    .HasForeignKey(e => e.ServiceId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Item).WithMany()
                    .HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Cascade);
            });

            // Вводим сущность Vehicle для хранения информации о транспортных средствах
            modelBuilder.Entity<Vehicle>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Vin).HasMaxLength(17);
                entity.Property(e => e.LicensePlate).HasMaxLength(15);
                entity.Property(e => e.Make).HasMaxLength(50);
                entity.Property(e => e.Model).HasMaxLength(50);
                entity.Property(e => e.Color).HasMaxLength(30);
                entity.Property(e => e.EngineType).HasMaxLength(20);
                entity.Property(e => e.Transmission).HasMaxLength(20);
                entity.Property(e => e.SellerFullName).HasMaxLength(150);
                entity.Property(e => e.SellerPhone).HasMaxLength(20);
                entity.Property(e => e.SellerPassport).HasMaxLength(50);
                entity.Property(e => e.BuyerFullName).HasMaxLength(150);
                entity.Property(e => e.BuyerPhone).HasMaxLength(20);
                entity.Property(e => e.BuyerPassport).HasMaxLength(50);
                entity.Property(e => e.Defects).HasMaxLength(2000);
                entity.Property(e => e.RepairNotes).HasMaxLength(2000);
                entity.Property(e => e.GeneralNotes).HasMaxLength(2000);

                entity.HasOne(e => e.Branch)
                      .WithMany()
                      .HasForeignKey(e => e.BranchId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Company)
                      .WithMany()
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}