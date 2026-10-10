using DaEtoZhe.Contracts.Enums;
using System;

namespace DaEtoZhe.Contracts.Models
{
    /// <summary>Строка дефектной ведомости автомобиля.</summary>
    public class VehicleDefect
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public DefectArea Area { get; set; }
        public DefectSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        /// <summary>Оценочная стоимость устранения, ₽.</summary>
        public decimal EstimatedCost { get; set; }
        /// <summary>Оценочная трудоёмкость, нормо-часов.</summary>
        public decimal EstimatedHours { get; set; }
        public bool IsFixed { get; set; }
        public DateTime? FixedAt { get; set; }
        public string Notes { get; set; } = string.Empty;

        public Vehicle Vehicle { get; set; }

        /// <summary>Зона по-русски (для UI, без конвертеров).</summary>
        public string AreaName
        {
            get
            {
                switch (Area)
                {
                    case DefectArea.Body: return "Кузов";
                    case DefectArea.Engine: return "Двигатель";
                    case DefectArea.Transmission: return "Трансмиссия";
                    case DefectArea.Suspension: return "Подвеска";
                    case DefectArea.Electrical: return "Электрика";
                    case DefectArea.Salon: return "Салон";
                    case DefectArea.Glass: return "Стёкла";
                    case DefectArea.Wheels: return "Колёса";
                    default: return "Прочее";
                }
            }
        }

        /// <summary>Критичность по-русски (для UI).</summary>
        public string SeverityName
        {
            get
            {
                switch (Severity)
                {
                    case DefectSeverity.Minor: return "Мелкий";
                    case DefectSeverity.Major: return "Средний";
                    default: return "Критичный";
                }
            }
        }
    }
}