using DaEtoZhe.Contracts.Enums;
using System;

namespace DaEtoZhe.Contracts.Models
{
    /// <summary>Автомобиль под ремонт и продажу.</summary>
    public class Vehicle
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int? BranchId { get; set; } // Где физически стоит

        // === Идентификация ===
        public string Vin { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;        // Марка (BMW, Toyota)
        public string Model { get; set; } = string.Empty;       // Модель (X5, Camry)
        public int Year { get; set; }
        public int Mileage { get; set; }                        // Пробег в км
        public string Color { get; set; } = string.Empty;
        public string EngineType { get; set; } = string.Empty;  // Бензин/Дизель/Электро
        public decimal EngineVolume { get; set; }               // Объем двигателя в л
        public string Transmission { get; set; } = string.Empty; // АКПП/МКПП/Робот/Вариатор

        // === Статус ===
        public VehicleStatus Status { get; set; } = VehicleStatus.New;

        // === Продавец (физлицо) ===
        public string SellerFullName { get; set; } = string.Empty;
        public string SellerPhone { get; set; } = string.Empty;
        public string SellerPassport { get; set; } = string.Empty; // Серия + номер
        public DateTime? PurchaseDate { get; set; }

        // === Финансы ===
        public decimal PurchasePrice { get; set; }      // Цена покупки
        public decimal RepairCost { get; set; }         // Стоимость ремонта (запчасти + работы)
        public decimal PartsCost { get; set; }          // Стоимость запчастей (из StockMovements)
        public decimal LaborCost { get; set; }          // Стоимость работ (нормо-часы)
        public decimal ListingPrice { get; set; }       // Цена в объявлении
        public decimal SalePrice { get; set; }          // Финальная цена продажи
        public decimal Margin { get { return SalePrice - PurchasePrice - RepairCost; } }

        // === Даты жизненного цикла ===
        public DateTime CreatedAt { get; set; }
        public DateTime? AppraisalDate { get; set; }
        public DateTime? RepairStartDate { get; set; }
        public DateTime? RepairEndDate { get; set; }
        public DateTime? ListedDate { get; set; }
        public DateTime? SoldDate { get; set; }

        // === Покупатель (физлицо) ===
        public string BuyerFullName { get; set; } = string.Empty;
        public string BuyerPhone { get; set; } = string.Empty;
        public string BuyerPassport { get; set; } = string.Empty;

        // === Заметки ===
        public string Defects { get; set; } = string.Empty;      // Описание дефектов
        public string RepairNotes { get; set; } = string.Empty;  // Что сделано
        public string GeneralNotes { get; set; } = string.Empty;

        // === Навигационные свойства ===
        public Branch Branch { get; set; }
        public Company Company { get; set; }
    }
}