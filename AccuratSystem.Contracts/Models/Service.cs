using System;
using System.Collections.Generic;
using AccuratSystem.Contracts.Enums;

namespace AccuratSystem.Contracts.Models
{
    /// <summary>
    /// Справочник услуг. Содержит базовую информацию, цены и категорию направления.
    /// PriceByBodyType сохраняется в PostgreSQL как jsonb через ValueConverter.
    /// </summary>
    public class Service
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Карта цен по типу кузова. Ключ: Id BodyType, Значение: Цена.
        /// Для услуг без привязки к кузову (автосервис) остаётся пустым.
        /// </summary>
        public Dictionary<int, decimal> PriceByBodyType { get; set; } = new Dictionary<int, decimal>();

        /// <summary>
        /// Флаг плавающей цены. Если true, цена устанавливается мастером при выполнении.
        /// </summary>
        public bool HasFloatingPrice { get; set; } = false;

        /// <summary>
        /// Рекомендуемая цена "от". Отображается в интерфейсе для ориентира.
        /// </summary>
        public decimal? BasePriceHint { get; set; }

        /// <summary>
        /// Категория направления услуги.
        /// </summary>
        public ServiceCategory ServiceCategory { get; set; } = ServiceCategory.Wash;

        /// <summary>
        /// Индивидуальный процент зарплаты за услугу. Null = использовать глобальную ставку.
        /// </summary>
        public decimal? CustomWagePercentage { get; set; }
    }
}